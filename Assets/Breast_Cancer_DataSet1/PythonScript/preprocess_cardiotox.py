import argparse
import numpy as np
import pandas as pd

CLINICAL_COLS = [
    "age","weight","height","CTRCD","time","LVEF","heart_rate","heart_rhythm",
    "PWT","LAd","LVDd","LVSd","AC","antiHER2","ACprev","antiHER2prev",
    "HTA","DL","DM","smoker","exsmoker","RTprev","CIprev","ICMprev","ARRprev",
    "VALVprev","cxvalv"
]

def read_csv(path: str) -> pd.DataFrame:
    try:
        df = pd.read_csv(path, sep=None, engine="python")
    except Exception:
        try:
            df = pd.read_csv(path, sep=";", engine="python", decimal=",", quotechar='"')
        except Exception:
            df = pd.read_csv(path, sep=None, engine="python", on_bad_lines="skip")
    keep = [c for c in CLINICAL_COLS if c in df.columns]
    if not keep:
        raise ValueError(f"No expected clinical columns found in {path}. Got: {list(df.columns)[:10]}")
    return df[keep].copy()


def coerce_numeric(df: pd.DataFrame) -> pd.DataFrame:
    for c in df.columns:
        df[c] = pd.to_numeric(df[c], errors="coerce")
    return df

def add_patient_id(df: pd.DataFrame) -> pd.DataFrame:
    df.insert(0, "patient_id", np.arange(1, len(df) + 1))
    return df

def add_bmi(df: pd.DataFrame) -> pd.DataFrame:
    h_m = df["height"] / 100.0
    df["BMI"] = df["weight"] / (h_m ** 2)
    df.loc[(df["BMI"] < 10) | (df["BMI"] > 60), "BMI"] = np.nan
    return df

def add_age_band(df: pd.DataFrame) -> pd.DataFrame:
    bins = [-np.inf, 50, 60, 70, np.inf]
    labels = ["<50","50-59","60-69","≥70"]
    df["age_band"] = pd.cut(df["age"], bins=bins, labels=labels, right=False)
    return df

def add_therapy_group(df: pd.DataFrame) -> pd.DataFrame:
    ac = df["AC"].fillna(0).astype(int)
    her2 = df["antiHER2"].fillna(0).astype(int)
    mapping = {
        (0,0): "none",
        (1,0): "AC_only",
        (0,1): "antiHER2_only",
        (1,1): "AC_plus_antiHER2"
    }
    df["therapy_group"] = [mapping[(a,h)] for a,h in zip(ac, her2)]
    df["prev_therapy_any"] = ((df["ACprev"].fillna(0) > 0) | (df["antiHER2prev"].fillna(0) > 0)).astype(int)
    return df

def add_comorbidity_score(df: pd.DataFrame) -> pd.DataFrame:
    cols = ["HTA","DL","DM","smoker","exsmoker","CIprev","ICMprev","ARRprev","VALVprev","cxvalv"]
    for c in cols:
        if c not in df.columns:
            df[c] = 0
    df["comorbidity_score"] = df[cols].fillna(0).astype(int).sum(axis=1)
    return df

def clean_values(df: pd.DataFrame) -> pd.DataFrame:
    ranges = {
        "age": (18, 95),
        "weight": (30, 200),
        "height": (120, 210),
        "LVEF": (10, 80),
        "heart_rate": (30, 220),
        "PWT": (0.5, 2.5),   # cm
        "LAd": (2.0, 6.0),   # cm
        "LVDd": (3.0, 7.5),  # cm
        "LVSd": (2.0, 6.0),  # cm
        "time": (0, 5000)    # days
    }
    for col, (lo, hi) in ranges.items():
        if col in df.columns:
            df.loc[(df[col] < lo) | (df[col] > hi), col] = np.nan
    if "heart_rhythm" in df.columns:
        df.loc[~df["heart_rhythm"].isin([0,1]), "heart_rhythm"] = np.nan
    bin_cols = ["CTRCD","AC","antiHER2","ACprev","antiHER2prev","HTA","DL","DM",
                "smoker","exsmoker","RTprev","CIprev","ICMprev","ARRprev","VALVprev","cxvalv"]
    for c in bin_cols:
        if c in df.columns:
            df.loc[~df[c].isin([0,1]), c] = np.nan
    return df

def finalize(df: pd.DataFrame) -> pd.DataFrame:
    export_cols = [
        "patient_id","age","age_band","weight","height","BMI",
        "LVEF","heart_rate","heart_rhythm",
        "PWT","LAd","LVDd","LVSd",
        "therapy_group","prev_therapy_any","comorbidity_score",
        "CTRCD","time"
    ]
    for c in export_cols:
        if c not in df.columns:
            df[c] = np.nan
    return df[export_cols].copy()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Path to BC_cardiotox_clinical_variables.csv")
    parser.add_argument("--output", required=True, help="Path to save cleaned csv")
    args = parser.parse_args()

    df = read_csv(args.input)
    df = coerce_numeric(df)
    df = add_patient_id(df)
    df = add_bmi(df)
    df = add_age_band(df)
    df = add_therapy_group(df)
    df = add_comorbidity_score(df)
    df = clean_values(df)
    out = finalize(df)

    out.to_csv(args.output, index=False)
    print(f"[OK] Saved cleaned table -> {args.output}")
    print(out.head(3).to_string(index=False))


if __name__ == "__main__":
    main()
