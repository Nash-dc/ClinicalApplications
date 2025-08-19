from fastapi import FastAPI
from pydantic import BaseModel
from typing import Dict, Any
from joblib import load
import pandas as pd
import numpy as np
import os

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
MODEL_DIR = os.path.normpath(os.path.join(BASE_DIR, "..", "model"))
MODEL_PATH = os.path.join(MODEL_DIR, "model_histgbdt.joblib")
FEAT_PATH  = os.path.join(MODEL_DIR, "model_features.csv")

app = FastAPI(title="CTRCD Risk Service")

model = load(MODEL_PATH)
features = pd.read_csv(FEAT_PATH, header=None)[0].tolist()

class Payload(BaseModel):
    data: Dict[str, Any]
    threshold: float | None = None

BASE_INPUTS = [
    "age","weight","height","LVEF","heart_rate","heart_rhythm",
    "PWT","LAd","LVDd","LVSd","AC","antiHER2","ACprev","antiHER2prev",
    "HTA","DL","DM","smoker","exsmoker","RTprev","CIprev","ICMprev",
    "ARRprev","VALVprev","cxvalv"
]

def to_float(v):
    if v is None: return None
    try: return float(v)
    except: return None

def build_row(d: Dict[str, Any]) -> pd.DataFrame:
    x = {k: to_float(d.get(k)) for k in BASE_INPUTS}
    if x.get("weight") and x.get("height"):
        h_m = x["height"]/100.0
        x["BMI"] = x["weight"]/(h_m*h_m) if h_m and h_m>0 else None
    else:
        x["BMI"] = None
    com_cols = ["HTA","DL","DM","smoker","exsmoker","CIprev","ICMprev","ARRprev","VALVprev","cxvalv"]
    com_vals = [x.get(c) for c in com_cols]
    x["comorbidity_score"] = float(np.nansum([vv for vv in com_vals if vv is not None]))

    lvef = x.get("LVEF")
    x["LVEF_low"] = 1.0 if (lvef is not None and lvef < 50) else 0.0
    x["LVEF_50_60"] = 1.0 if (lvef is not None and 50 <= lvef < 60) else 0.0
    x["LVEF_ge60"] = 1.0 if (lvef is not None and lvef >= 60) else 0.0
    x["LVEF_low_x_AC"] = x["LVEF_low"] * (x.get("AC") or 0.0)
    x["LVEF_low_x_antiHER2"] = x["LVEF_low"] * (x.get("antiHER2") or 0.0)

    row = {f: x.get(f, None) for f in features}
    return pd.DataFrame([row], columns=features)

@app.get("/health")
def health():
    return {"status":"ok","features": len(features)}

@app.post("/predict")
def predict(p: Payload):
    df = build_row(p.data)
    prob = float(model.predict_proba(df)[0,1])
    thr = p.threshold if p.threshold is not None else 0.30
    pred = 1 if prob >= thr else 0

    extra = {
        "LVEF": p.data.get("LVEF", None),
        "BMI": df.iloc[0].get("BMI", None),
        "comorbidity_score": df.iloc[0].get("comorbidity_score", None)
    }
    return {"prob": prob, "pred": pred, "threshold": thr, "echo": extra, "feature_count": len(features)}
