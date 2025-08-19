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
from sklearn.ensemble import RandomForestClassifier, HistGradientBoostingClassifier, GradientBoostingClassifier
from sklearn.utils.class_weight import compute_class_weight

from joblib import dump

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
    if "LVEF" in X.columns:
        X["LVEF_low"] = (X["LVEF"] < 50).astype(float)
        X["LVEF_50_60"] = ((X["LVEF"] >= 50) & (X["LVEF"] < 60)).astype(float)
        X["LVEF_ge60"] = (X["LVEF"] >= 60).astype(float)
        if "AC" in X.columns:
            X["LVEF_low_x_AC"] = X["LVEF_low"] * X["AC"]
        if "antiHER2" in X.columns:
            X["LVEF_low_x_antiHER2"] = X["LVEF_low"] * X["antiHER2"]
    return X, y, list(X.columns)

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

def fit_random_forest(X_train, y_train, random_state=42):
    rf = Pipeline([
        ("imp", SimpleImputer(strategy="median")),
        ("clf", RandomForestClassifier(
            n_estimators=400,
            max_depth=None,
            min_samples_leaf=3,
            class_weight="balanced",
            random_state=random_state,
            n_jobs=-1
        ))
    ])
    rf.fit(X_train, y_train)
    return rf

def fit_hist_gbdt(X_train, y_train, random_state=42):
    mono = np.zeros(X_train.shape[1], dtype=int)
    if "LVEF" in X_train.columns:
        mono[list(X_train.columns).index("LVEF")] = -1
    hgb = Pipeline([
        ("imp", SimpleImputer(strategy="median")),
        ("clf", HistGradientBoostingClassifier(
            learning_rate=0.05,
            max_depth=None,
            max_leaf_nodes=31,
            min_samples_leaf=20,
            random_state=random_state,
            monotonic_cst=mono
        ))
    ])
    hgb.fit(X_train, y_train)
    return hgb

def try_fit_xgboost(X_train, y_train, random_state=42):
    try:
        import xgboost as xgb
    except Exception:
        return None
    y_np = np.asarray(y_train, dtype=int)
    pos = (y_np == 1).sum()
    neg = (y_np == 0).sum()
    spw = max(1.0, neg / max(1, pos))
    xgb_clf = Pipeline([
        ("imp", SimpleImputer(strategy="median")),
        ("clf", xgb.XGBClassifier(
            n_estimators=500,
            max_depth=4,
            learning_rate=0.05,
            subsample=0.9,
            colsample_bytree=0.9,
            reg_lambda=1.0,
            reg_alpha=0.0,
            scale_pos_weight=spw,
            eval_metric="logloss",
            random_state=random_state,
            n_jobs=-1
        ))
    ])
    xgb_clf.fit(X_train, y_train)
    return xgb_clf

def evaluate_any(estimator, X_test, y_test, outdir, name):
    proba = None
    if hasattr(estimator, "predict_proba"):
        proba = estimator.predict_proba(X_test)[:, 1]
    elif hasattr(estimator, "decision_function"):
        scores = estimator.decision_function(X_test)
        s_min, s_max = scores.min(), scores.max()
        proba = (scores - s_min) / (s_max - s_min + 1e-9)
    else:
        proba = estimator.predict(X_test).astype(float)
    roc = roc_auc_score(y_test, proba)
    pr = average_precision_score(y_test, proba)
    pred05 = (proba >= 0.5).astype(int)
    report05 = classification_report(y_test, pred05, digits=3)
    prec, rec, thr = precision_recall_curve(y_test, proba)
    f1 = 2 * prec * rec / (prec + rec + 1e-9)
    best_idx = int(np.nanargmax(f1))
    best_thr = thr[best_idx-1] if best_idx > 0 and best_idx-1 < len(thr) else 0.5
    pred_best = (proba >= best_thr).astype(int)
    report_best = classification_report(y_test, pred_best, digits=3)
    fpr, tpr, _ = roc_curve(y_test, proba)
    plt.figure(figsize=(10,4))
    plt.subplot(1,2,1)
    plt.plot(fpr, tpr, label=f"AUC={roc:.3f}")
    plt.plot([0,1],[0,1],'--',alpha=.4)
    plt.xlabel("FPR"); plt.ylabel("TPR"); plt.title(f"ROC - {name}"); plt.legend()
    plt.subplot(1,2,2)
    plt.plot(rec, prec, label=f"AP={pr:.3f}")
    plt.xlabel("Recall"); plt.ylabel("Precision"); plt.title(f"PR - {name}"); plt.legend()
    plt.tight_layout()
    plt.savefig(os.path.join(outdir, f"{name}_roc_pr.png"), dpi=160)
    plt.close()
    with open(os.path.join(outdir, f"{name}_metrics.txt"), "w", encoding="utf-8") as f:
        f.write(f"Model: {name}\n")
        f.write(f"ROC-AUC: {roc:.4f}\nPR-AUC: {pr:.4f}\n")
        f.write(f"\n=== Report @ threshold 0.5 ===\n{report05}\n")
        f.write(f"\n=== Report @ best-F1 threshold={best_thr:.3f} ===\n{report_best}\n")
    return {
        "name": name,
        "roc_auc": roc,
        "pr_auc": pr,
        "best_thr": float(best_thr)
    }

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Path to BC_cardiotox_clinical_variables.csv")
    parser.add_argument("--outdir", required=True, help="Directory to save outputs")
    args = parser.parse_args()
    os.makedirs(args.outdir, exist_ok=True)
    df_raw = robust_read_csv(args.input)
    keep = [c for c in CLINICAL_COLS if c in df_raw.columns]
    if not keep:
        raise ValueError("No expected clinical columns found in input.")
    df_raw = df_raw[keep].copy()
    X, y, feature_cols = build_feature_matrix(df_raw)
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42, stratify=y
    )
    or_table, imp_sm, sm_res = fit_statsmodels_logit(X_train, y_train, feature_cols, args.outdir)
    results = []
    logit_pipe = fit_sklearn_logit(X_train, y_train)
    results.append(evaluate_any(logit_pipe, X_test, y_test, args.outdir, "logistic"))
    rf = fit_random_forest(X_train, y_train)
    results.append(evaluate_any(rf, X_test, y_test, args.outdir, "random_forest"))
    hgb = fit_hist_gbdt(X_train, y_train)
    results.append(evaluate_any(hgb, X_test, y_test, args.outdir, "hist_gbdt"))
    xgb_model = try_fit_xgboost(X_train, y_train)
    if xgb_model is not None:
        results.append(evaluate_any(xgb_model, X_test, y_test, args.outdir, "xgboost"))
    res_df = pd.DataFrame(results)
    res_df.to_csv(os.path.join(args.outdir, "model_compare.csv"), index=False)
    pd.Series(feature_cols, name="feature").to_csv(
        os.path.join(args.outdir, "model_features.csv"), index=False
    )
    print("\n=== OR table (前几行) ===")
    print(or_table.head().to_string(index=False))
    print("\n=== Model comparison (ROC-AUC / PR-AUC) ===")
    print(res_df.sort_values("roc_auc", ascending=False).to_string(index=False))
    print("\n说明：各模型详细指标见 *_metrics.txt；曲线见 *_roc_pr.png。")
    print("阈值在 *_metrics.txt 中含 0.5 与 best-F1 两种参考，真正部署应在验证集单独调参。")

    #Store the model
    dump(hgb, "../model/model_histgbdt.joblib")
    pd.Series(feature_cols, name="feature").to_csv("../model/model_features.csv", index=False, header=False)

if __name__ == "__main__": 
    main()
