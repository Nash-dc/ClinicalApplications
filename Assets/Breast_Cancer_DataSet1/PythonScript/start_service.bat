@echo off
cd /d %~dp0

echo === Checking virtual environment ===
if not exist "..\venv\" (
    echo venv not found. Creating virtual environment...
    python -m venv ..\venv
    call ..\venv\Scripts\activate
    echo Installing requirements...
    pip install --upgrade pip
    pip install -r requirements.txt
) else (
    echo venv found.
    call ..\venv\Scripts\activate
)

echo === Starting FastAPI service on http://127.0.0.1:8000 ===
uvicorn serve_predict:app --reload --host 127.0.0.1 --port 8000

pause
