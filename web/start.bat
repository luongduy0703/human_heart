@echo off
rem Chay trang AR bang webcam: mo web server o thu muc goc cua repo (de doc duoc Assets/)
rem roi mo trinh duyet vao http://localhost:8000/web/ . Dong cua so nay de tat server.
cd /d "%~dp0.."
set PORT=8000
set PY=
where python >nul 2>nul && set PY=python
if not defined PY where py >nul 2>nul && set PY=py
if not defined PY (
  echo Khong tim thay Python. Cai Python tu https://www.python.org/ roi chay lai.
  pause
  exit /b 1
)
echo Web server: http://localhost:%PORT%/web/
echo Dong cua so nay de tat server.
start "" cmd /c "timeout /t 2 /nobreak >nul & start http://localhost:%PORT%/web/"
%PY% -m http.server %PORT% --bind 127.0.0.1
pause
