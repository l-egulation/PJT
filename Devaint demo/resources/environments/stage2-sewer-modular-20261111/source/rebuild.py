from pathlib import Path
import subprocess,sys
ROOT=Path(__file__).resolve().parents[1]
for script in ["stage2-unified-stone-v21/source/build.py","stage2-unified-map-review-v22/source/build_review.py","stage2-room-background-v24/source/export_foreground.py","stage2-room-background-v24/source/build.py","stage2-sewer-modern-v25/source/build.py","stage2-sewer-modern-v26/source/build.py"]:
 subprocess.run([sys.executable,str(ROOT/script)],check=True)
subprocess.run([sys.executable,str(ROOT/"source/verify.py")],check=True)
