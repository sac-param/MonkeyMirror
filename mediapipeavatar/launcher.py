import subprocess
import time
import threading

# ── CONFIG ────────────────────────────────────────────────────
MEDIAPIPE_DIR    = r"C:\SAC\UnityPythonMediaPipeAvatar-NEW\mediapipeavatar"
VENV_PYTHON      = r"C:\SAC\UnityPythonMediaPipeAvatar-NEW\mediapipeavatar\venv\Scripts\python.exe"
MEDIAPIPE_SCRIPT = r"main.py"
UNITY_EXE        = r"C:\SAC\UnityPythonMediaPipeAvatar-NEW\UnityMediaPipeAvatar\Build\MonkeyMirror.exe"

WAIT_BEFORE_UNITY = 20
RESTART_DELAY = 3
# ─────────────────────────────────────────────────────────────


def kill_existing_mediapipe():
    print("[~] Killing old MediaPipe main.py processes...")

    ps_cmd = r'''
    Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq "python.exe" -and
        $_.CommandLine -like "*main.py*"
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force
    }
    '''

    try:
        subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ps_cmd],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL
        )
        print("[✓] Old MediaPipe processes cleared.")
    except Exception as e:
        print("[!] Could not kill old process:", e)

    time.sleep(1)


def stream_output(proc):
    print("\n[✓] main.py live logs started...\n", flush=True)

    try:
        for line in iter(proc.stdout.readline, ''):
            if line:
                print(f"[MAIN.PY] {line.rstrip()}", flush=True)
    except Exception as e:
        print(f"[!] Log stream error: {e}", flush=True)

    print("\n[!] main.py log stream ended.", flush=True)


def start_mediapipe():
    print("\n[1] Starting MediaPipe...")

    proc = subprocess.Popen(
        [VENV_PYTHON, "-u", MEDIAPIPE_SCRIPT],
        cwd=MEDIAPIPE_DIR,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        stdin=subprocess.DEVNULL,
        text=True,
        bufsize=1
    )

    print(f"[✓] MediaPipe started. PID: {proc.pid}")

    t = threading.Thread(target=stream_output, args=(proc,), daemon=True)
    t.start()

    return proc


def start_unity():
    print("\n[3] Launching Unity game...")
    proc = subprocess.Popen([UNITY_EXE])
    print(f"[✓] Unity launched. PID: {proc.pid}")
    return proc


def main():
    print("=" * 50)
    print("       MONKEY MIRROR LAUNCHER")
    print("=" * 50)

    kill_existing_mediapipe()

    mediapipe_proc = start_mediapipe()

    print(f"\n[2] Waiting {WAIT_BEFORE_UNITY} seconds before launching Unity...")
    for i in range(WAIT_BEFORE_UNITY, 0, -1):
        print(f"    {i}...")
        time.sleep(1)

        if mediapipe_proc.poll() is not None:
            print("[!] MediaPipe crashed before Unity launch. Restarting...")
            mediapipe_proc = start_mediapipe()
            print(f"[~] Waiting {WAIT_BEFORE_UNITY} seconds again...")
            time.sleep(WAIT_BEFORE_UNITY)
            break

    unity_proc = start_unity()

    print("\n[✓] All systems running. Press Ctrl+C to stop.\n")

    try:
        while True:
            if mediapipe_proc.poll() is not None:
                print("\n[!] MediaPipe crashed/stopped.")
                print(f"[~] Restarting MediaPipe in {RESTART_DELAY} seconds...")
                time.sleep(RESTART_DELAY)

                mediapipe_proc = start_mediapipe()
                print("[✓] MediaPipe restarted successfully.")

            if unity_proc.poll() is not None:
                print("\n[!] Unity crashed/stopped.")
                print("[~] Restarting Unity...")
                unity_proc = start_unity()
                print("[✓] Unity restarted successfully.")

            time.sleep(2)

    except KeyboardInterrupt:
        print("\n[!] Shutting down...")

        try:
            mediapipe_proc.terminate()
        except:
            pass

        try:
            unity_proc.terminate()
        except:
            pass

        print("[✓] All stopped.")


if __name__ == "__main__":
    main()