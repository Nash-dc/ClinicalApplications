# Clinical Applications
---

## 📂 Project Structure

```
ClinicalApplications/
│
├── Assets/
│   └── Breast_Cancer_DataSet1/
│       ├── PythonScript/         # Python backend (FastAPI service + model training)
│       │   ├── train_and_eval.py
│       │   ├── serve_model.py
│       │   ├── start_service.bat
│       │   ├── requirements.txt
│       │   └── ...
│       └── BC_cardiotox_clinical_variables.csv
│
├── Models/
│   │   ├── Patient.cs
│   │   └── CtrcdRiskClient.cs
├── Views/
│── ViewModels/
└── ClinicalApplications.csproj
│
├── README.md                     # This file
└── .gitignore
```

---

## ⚙️ Requirements

- **Python 3.10+**
- **.NET 9.0 SDK**
- **Visual Studio 2022** (Community edition is fine)
- **Node.js** (optional, for Avalonia Designer hot reload)

---

## 🐍 Backend (Python, FastAPI)

1. Navigate to the PythonScript folder:

   ```powershell
   cd Assets\Breast_Cancer_DataSet1\PythonScript
   ```

2. Start the backend service:

   ```powershell
   start_service.bat
   ```

   - If `venv` does not exist, it will be automatically created and dependencies from `requirements.txt` will be installed.  
   - The service runs on **http://127.0.0.1:8000**.  
   - Endpoints:  
     - `POST /predict` → make predictions  
     - `GET /health` → check service status  

---

## 💻 Frontend (Avalonia, C#)

### Step 1 – Install Visual Studio

- Download **Visual Studio 2022 Community**:  
  👉 https://visualstudio.microsoft.com/downloads/

- During installation, select workload:
  - **.NET desktop development**
  - **Desktop development with C++** (optional but useful)

### Step 2 – Install Avalonia Extension

- In Visual Studio → `Extensions > Manage Extensions > Online`  
- Search for **Avalonia for Visual Studio** and install.  
- Restart Visual Studio.

### Step 3 – Open Project

- Open solution file `ClinicalApplications.sln`.  
- Right-click project → `Set as Startup Project`.  
- Run with **F5**.  

---

## 🔑 Note on GPTKey

This project uses GPT-based components, but **the GPTKey is not included** in this repository for security reasons.  

---

## 📊 Model Training and Saving

- Models are trained inside `train_and_eval.py`.  
- Trained models are saved as `.pkl` files under `PythonScript/models/`.  
- `serve_model.py` automatically loads the latest trained model when the service starts.  

---

## 📚 References

If you use this repository or the cardiotoxicity risk model, please cite:

**APA format**  
Piñeiro-Lamas, B., López-Cheda, A., Cao, R., Ramos-Alonso, L., González-Barbeito, G., Barbeito-Caamaño, C., & Bouzas-Mosquera, A. (2023). *BC_cardiotox: A cardiotoxicity dataset for breast cancer patients*. [Dataset].  

**IEEE format**  
B. Piñeiro-Lamas, A. López-Cheda, R. Cao, L. Ramos-Alonso, G. González-Barbeito, C. Barbeito-Caamaño, and A. Bouzas-Mosquera, “BC_cardiotox: A cardiotoxicity dataset for breast cancer patients,” 2023. [Dataset].

---

## 📖 License

This repository is for **academic and research purposes only**.  
Please cite the related paper if you use any part of this code.
