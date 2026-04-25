#pipe server
from body import BodyThread
import time
import struct
import global_vars
from sys import exit

thread = BodyThread()
thread.start()

# Keep running until Ctrl+C
try:
    while True:
        time.sleep(1)
except KeyboardInterrupt:
    print("Exiting…")
    global_vars.KILL_THREADS = True
time.sleep(0.5)
exit()