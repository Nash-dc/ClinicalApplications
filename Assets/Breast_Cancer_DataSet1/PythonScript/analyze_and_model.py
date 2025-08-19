import argparse, os, json
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt

from sklearn.model_selection import train_test_split
from sklearn.impute import SimpleImputer
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import Pipeline
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import roc_auc_score, average_precision_score, roc_curve, precision_recall_curve, classification_report

import statsmodels.api as sm

CLINICAL_COLS = [
    "age","weight","height","CTRCD","time","LVEF","heart_rate","heart_rhythm",
    "PWT","LAd","LVDd","LVSd","AC","antiHER2","ACprev","antiHER2prev",
    "HTA","DL","DM","smoker","exsmoker","RTprev","CIprev","ICMprev","ARRprev",
    "VALVprev","cxvalv"
]

BIN_COLS = ["CTRCD","heart_rhythm","AC","antiHER2","ACprev","antiHER2prev","HTA","DL","DM",
            "smoker","exsmoker","RTprev","CIprev","ICMprev","ARRprev","VALVprev","cxvalv"]

def robust_read_csv(path: str) -> pd.DataFrame:
    try:
        df = pd.read_csv(path, sep=None, engine="python")
    except Exception:
        try:
            df = pd.read_csv(path, sep=";", engine="python", decimal=",", quotechar='"')
        except Exception:
            df = pd.read_csv(path, sep=None, engine="python", on_bad_lines="skip")
    return df

def coerce_numeric(df: pd.DataFrame) -> pd.DataFrame:
    for c in df.columns:
        df[c] = pd.to_numeric(df[c], errors="coerce")
    return df

def add_bmi(df: pd.DataFrame) -> pd.DataFrame:
    if "weight" in df.columns and "height" in df.columns:
        h_m = df["height"] / 100.0
        df["BMI"] = df["weight"] / (h_m ** 2)
        df.loc[(df["BMI"] < 10) | (df["BMI"] > 60), "BMI"] = np.nan
    return df

def add_comorbidity_score(df: pd.DataFrame) -> pd.DataFrame:
    cols = ["HTA","DL","DM","smoker","exsmoker","CIprev","ICMprev","ARRprev","VALVprev","cxvalv"]
    for c in cols:
        if c not in df.columns:
            df[c] = np.nan
    df["comorbidity_score"] = df[cols].fillna(0).astype(float).sum(axis=1)
    return df

def clean_ranges(df: pd.DataFrame) -> pd.DataFrame:
    ranges = {
        "age": (18, 95), "weight": (30, 200), "height": (120, 210),
        "LVEF": (10, 80), "heart_rate": (30, 220),
        "PWT": (0.5, 2.5), "LAd": (2.0, 6.0), "LVDd": (3.0, 7.5), "LVSd": (2.0, 6.0)
    }
    for col,(lo,hi) in ranges.items():
        if col in df.columns:
            df.loc[(df[col] < lo) | (df[col] > hi), col] = np.nan
    for c in BIN_COLS:
        if c in df.columns:
            df.loc[~df[c].isin([0,1]), c] = np.nan
    return df

def build_feature_matrix(df_raw: pd.DataFrame):

    keep = [c for c in CLINICAL_COLS if c in df_raw.columns]
    df = df_raw[keep].copy()

    df = coerce_numeric(df)

    df = add_bmi(df)
    df = add_comorbidity_score(df)
    df = clean_ranges(df)

    if "CTRCD" not in df.columns:
        raise ValueError("CTRCD column missing.")
    y = df["CTRCD"].astype(float)

    feature_cols = [
        "age","BMI","LVEF","heart_rate","heart_rhythm",
        "PWT","LAd","LVDd","LVSd",
        "AC","antiHER2","ACprev","antiHER2prev",
        "HTA","DL","DM","smoker","exsmoker","RTprev",
        "CIprev","ICMprev","ARRprev","VALVprev","cxvalv",
        "comorbidity_score"
    ]
    feature_cols = [c for c in feature_cols if c in df.columns]
    X = df[feature_cols].astype(float)

    return X, y, feature_cols

def fit_statsmodels_logit(X_train, y_train, feature_cols, outdir):

    imp = SimpleImputer(strategy="median")
    X_imp = imp.fit_transform(X_train)

    X_imp_df = pd.DataFrame(X_imp, columns=feature_cols)
    nunique = X_imp_df.nunique(dropna=True)
    keep_mask = nunique > 1
    if not keep_mask.all():
        dropped = list(X_imp_df.columns[~keep_mask])
        X_imp_df = X_imp_df.loc[:, keep_mask]
        print(f"[warn] Dropped near-constant columns (statsmodels): {dropped}")


    X_sm = sm.add_constant(X_imp_df, has_constant='add')

    try:
        model = sm.Logit(y_train.values, X_sm, missing='raise')
        res = model.fit(disp=False, method='lbfgs', maxiter=200)
    except Exception as e:

        print(f"[warn] Logit failed with all features: {e}")
        topk = min(8, X_sm.shape[1]-1) 
        X_sm = sm.add_constant(X_imp_df.iloc[:, :topk], has_constant='add')
        model = sm.Logit(y_train.values, X_sm, missing='raise')
        res = model.fit(disp=False, method='lbfgs', maxiter=200)

    params = res.params
    conf = res.conf_int() 

    or_series = np.exp(params)
    or_low = np.exp(conf[0])
    or_high = np.exp(conf[1])
    p_vals = res.pvalues
    rows = []
    for name in params.index:
        rows.append({
            "feature": name,
            "OR": or_series.loc[name],
            "CI_low": or_low.loc[name],
            "CI_high": or_high.loc[name],
            "p_value": p_vals.loc[name]
        })
    or_table = pd.DataFrame(rows)

    or_table.to_csv(os.path.join(outdir, "or_table.csv"), index=False)
    return or_table, imp, res


def fit_sklearn_logit(X_train, y_train):

    pipe = Pipeline([
        ("imp", SimpleImputer(strategy="median")),
        ("scaler", StandardScaler(with_mean=True, with_std=True)),
        ("clf", LogisticRegression(max_iter=200, class_weight="balanced", solver="lbfgs"))
    ])
    pipe.fit(X_train, y_train)
    return pipe

def evaluate_model(pipe, X_test, y_test, outdir):
    proba = pipe.predict_proba(X_test)[:,1]
    roc = roc_auc_score(y_test, proba)
    pr = average_precision_score(y_test, proba)

    pred = (proba >= 0.5).astype(int)
    report = classification_report(y_test, pred, digits=3)

    fpr, tpr, _ = roc_curve(y_test, proba)
    prec, rec, _ = precision_recall_curve(y_test, proba)

    plt.figure(figsize=(10,4))
    plt.subplot(1,2,1)
    plt.plot(fpr, tpr, label=f"ROC AUC={roc:.3f}")
    plt.plot([0,1],[0,1],'--',alpha=.4)
    plt.xlabel("FPR"); plt.ylabel("TPR"); plt.title("ROC"); plt.legend()

    plt.subplot(1,2,2)
    plt.plot(rec, prec, label=f"PR AUC={pr:.3f}")
    plt.xlabel("Recall"); plt.ylabel("Precision"); plt.title("PR"); plt.legend()

    plt.tight_layout()
    plt.savefig(os.path.join(outdir, "roc_pr.png"), dpi=160)
    plt.close()

    with open(os.path.join(outdir, "metrics.txt"), "w", encoding="utf-8") as f:
        f.write(f"ROC-AUC: {roc:.4f}\nPR-AUC: {pr:.4f}\n\n")
        f.write(report)

    return roc, pr, report

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Path to BC_cardiotox_clinical_variables.csv")
    parser.add_argument("--outdir", required=True, help="Directory to save outputs")
    args = parser.parse_args()

    os.makedirs(args.outdir, exist_ok=True)

    df_raw = robust_read_csv(args.input)

    keep = [c for c in CLINICAL_COLS if c in df_raw.columns]
    df_raw = df_raw[keep].copy()

    X, y, feature_cols = build_feature_matrix(df_raw)


    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42, stratify=y
    )


    or_table, imp_sm, sm_res = fit_statsmodels_logit(X_train, y_train, feature_cols, args.outdir)


    pipe = fit_sklearn_logit(X_train, y_train)
    roc, pr, report = evaluate_model(pipe, X_test, y_test, args.outdir)


    pd.Series(feature_cols, name="feature").to_csv(os.path.join(args.outdir, "model_features.csv"), index=False)

    print("\n=== OR table (前几行) ===")
    print(or_table.head().to_string(index=False))
    print("\n=== Metrics ===")
    print(f"ROC-AUC={roc:.3f}  PR-AUC={pr:.3f}")
    print(report)

    print("示例风险分层：p<0.10 低风险；0.10–0.25 中等；>0.25 高风险（可按需要校准）")

if __name__ == "__main__":
    main()
