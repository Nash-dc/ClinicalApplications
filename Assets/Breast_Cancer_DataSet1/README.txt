BC_cardiotox: A cardiotoxicity dataset for breast cancer patients

Authors: Beatriz Piñeiro-Lamas, Ana López-Cheda, Ricardo Cao, Laura Ramos-Alonso, Gabriel González-Barbeito, Cayetana Barbeito-Caamaño, Alberto Bouzas-Mosquera

Corresponding author: Beatriz Piñeiro-Lamas (b.pineiro.lamas@udc.es)

Files: 

* BC_cardiotox_clinical_variables.csv: dataset with 531 rows (patients) and 27 columns (clinical variables). 
  The column headers are age (in years), weight (in kg), height (in cm), CTRCD (1 if CTRCD experienced during the follow-up period,  0 otherwise) , time (time, in days, from beginnig of treatment to CTRCD or end of follow-up), LVEF (Left Ventricular Ejection Fraction, in %), heart_rate (in lpm), heart_rhythm (0 = sinus rhythm, 1 = atrial fibrillation), PWT (Posterior Wall Thickness, in cm), LAd (Left Atrial diameter, in cm), LVDd (Left Ventricular Diastolic diameter, in cm), LVSd (Left Ventricular Systolic diameter, in cm), AC (anthracyclines; 0 = no, 1 = yes), antiHER2 (anti-HER2 therapies; 0 = no, 1 = yes), ACprev (previous anthracyclines; 0 = no, 1 = yes), antiHER2prev (previous anti-HER2 therapies; 0 = no, 1 = yes), HTA (hypertension; 0 = no, 1 = yes), DL (dyslipidemia; 0 = no, 1 = yes), DM (diabetes mellitus; 0 = no, 1 = yes), smoker (0 = no, 1 = yes), exsmoker (0 = no, 1 = yes), RTprev (previous thorax radiotherapy; 0 = no, 1 = yes), CIprev (previous cardiac insufficiency; 0 = no, 1 = yes), 
  ICMprev (previous ischemic cardiomyopathy; 0 = no, 1 = yes), ARRprev (previous arrhythmia; 0 = no, 1 = yes), VALVprev (previous valvulopathy; 0 = no, 1 = yes) and cxvalv (previous valve surgery; 0 = no, 1 = yes).
* BC_cardiotox_functional_variable.csv: dataset with 270 rows (patients) and 1002 columns. The first column is CTRCD, and the remaining 1001 (named t 1, t 2, ..., t 1000, t 1001) contain the cycle extracted from the TDI discretized in 1001 equispaced points in the interval [0,1].
* BC_cardiotox_clinical_and_functional_variables.csv: dataset with 531 rows (patients) and 1028 columns. The first 27 columns are the same as in BC_cardiotox_clinical_variables, and the remaining are  t 1, t2, ..., t 1000, t 1001.  For the patients whose image has not been preprocessed, the last 1001 columns contain NAs.
* TDI_images_preprocessing_algorithm.R: commented R code for the preprocessing of image data.
* TDIimage_automatic.png: example of a TDI image whose preprocessing is automatic.
* TDIimage_manual.png: example of a TDI image whose preprocessing requires a manual selection of the beginning and end points of the cycle. 
* IMAGES.rar: 270 TDI images.

