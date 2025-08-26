import argparse
import os
import pandas as pd
import numpy as np


PATIENT_FIELDS = [
    "age","weight","height","LVEF","heart_rate","heart_rhythm",
    "PWT","LAd","LVDd","LVSd",
    "AC","antiHER2","ACprev","antiHER2prev",
    "HTA","DL","DM","smoker","exsmoker",
    "RTprev","CIprev","ICMprev","ARRprev","VALVprev","cxvalv"
]

ALIASES = {
    "age": ["age"],
    "weight": ["weight"],
    "height": ["height"],
    "LVEF": ["lvef","LVEF"],
    "heart_rate": ["heart_rate","hr","heart rate"],
    "heart_rhythm": ["heart_rhythm","rhythm","heart rhythm"],
    "PWT": ["PWT","posterior wall thickness"],
    "LAd": ["LAd","left atrial diameter","la_d"],
    "LVDd": ["LVDd","lvdd","left ventricular diastolic diameter"],
    "LVSd": ["LVSd","lvsd","left ventricular systolic diameter"],
    "AC": ["AC","anthracyclines","anthracycline"],
    "antiHER2": ["antiHER2","anti-her2","anti_her2","trastuzumab"],
    "ACprev": ["ACprev","prev_ac","previous anthracyclines"],
    "antiHER2prev": ["antiHER2prev","prev_antiher2","previous anti-her2"],
    "HTA": ["HTA","hypertension"],
    "DL": ["DL","dyslipidemia","hyperlipidemia"],
    "DM": ["DM","diabetes","diabetes mellitus"],
    "smoker": ["smoker","current_smoker"],
    "exsmoker": ["exsmoker","former_smoker"],
    "RTprev": ["RTprev","previous thorax radiotherapy","thorax_rt_prev"],
    "CIprev": ["CIprev","cardiac insufficiency prev","hf_prev","heart failure prev"],
    "ICMprev": ["ICMprev","ischemic cardiomyopathy prev","cad_prev"],
    "ARRprev": ["ARRprev","arrhythmia prev"],
    "VALVprev": ["VALVprev","valvulopathy prev"],
    "cxvalv": ["cxvalv","valve surgery prev","prev valve surgery"]
}

def robust_read_csv(path: str) -> pd.DataFrame:
    try:
        return pd.read_csv(path, sep=None, engine="python")
    except Exception:
        try:
            return pd.read_csv(path, sep=";", engine="python", decimal=",", quotechar='"')
        except Exception:
            return pd.read_csv(path, sep=None, engine="python", on_bad_lines="skip")

def coerce_numeric_series(s: pd.Series) -> pd.Series:

    if s.dtype.kind in "biufc":  
        return s

    s2 = s.astype(str).str.replace(",", ".", regex=False)
    return pd.to_numeric(s2, errors="coerce")

def unify_columns(df: pd.DataFrame) -> pd.DataFrame:

    normalize = lambda x: x.strip().lower().replace("\n", " ").replace("\r", " ")
    norm_to_raw = {normalize(c): c for c in df.columns}

    selected = {}
    for target in PATIENT_FIELDS:
        picked_col = None
        if target in df.columns:
            picked_col = target
        else:
            for alias in ALIASES.get(target, []):
                key = normalize(alias)
                if key in norm_to_raw:
                    picked_col = norm_to_raw[key]
                    break
        if picked_col is not None:
            selected[target] = coerce_numeric_series(df[picked_col])
        else:
            selected[target] = np.nan  # 缺失列补 NaN

    out = pd.DataFrame(selected)
    bin_cols = [
        "heart_rhythm","AC","antiHER2","ACprev","antiHER2prev","HTA","DL","DM",
        "smoker","exsmoker","RTprev","CIprev","ICMprev","ARRprev","VALVprev","cxvalv"
    ]
    for c in bin_cols:
        if c in out.columns:
            out.loc[~out[c].isin([0,1]), c] = np.nan
    return out

def export_per_patient(df: pd.DataFrame, outdir: str, prefix: str = "patient"):
    os.makedirs(outdir, exist_ok=True)
    index_rows = []
    for i, row in df.iterrows():
        fname = f"{prefix}_{i+1:04d}.csv"
        fpath = os.path.join(outdir, fname)
        row_df = row.to_frame().T  # (1, N)
        row_df.to_csv(fpath, index=False)
        index_rows.append({"row_index": i, "file": fname})

    pd.DataFrame(index_rows).to_csv(os.path.join(outdir, "index.csv"), index=False)
    return len(index_rows)

def main():
    ap = argparse.ArgumentParser(description="Export each patient as a standalone CSV matching Patient class fields.")
    ap.add_argument("--input", required=True, help="Path to BC_cardiotox_clinical_variables.csv")
    ap.add_argument("--outdir", required=True, help="Output directory to store per-patient CSVs")
    ap.add_argument("--prefix", default="patient", help="Filename prefix, default 'patient'")
    args = ap.parse_args()

    df_raw = robust_read_csv(args.input)
    df_uni = unify_columns(df_raw)

    n = export_per_patient(df_uni, args.outdir, prefix=args.prefix)
    print(f"[OK] Exported {n} patient CSV files to: {args.outdir}")
    print(f"[OK] Index saved at: {os.path.join(args.outdir, 'index.csv')}")

if __name__ == "__main__":
    main()
