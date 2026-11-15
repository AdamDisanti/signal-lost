# Builds the desert bosses in Blender: model, skeleton, animation clips, the FBX for Unity, the clip timing table
# (JSON, read by the boss scripts so their hit timings match the animation), the .blend and preview renders.
#   blender --background --python tools/blender/desert_bosses.py -- wyvern drake scorpion
# or through the Blender MCP: set BOSSES = [...] and run this file's text.
import sys, os, importlib, traceback

REPO = os.environ.get("SIGNAL_LOST_REPO", r"C:\Users\Lawson\signal-lost")
HERE = os.path.join(REPO, "tools", "blender")
if HERE not in sys.path:
    sys.path.insert(0, HERE)

OUT = os.path.join(REPO, "signal-lost", "Assets", "Prefabs", "Enemies", "Desert", "Bosses")
ART = os.path.join(REPO, "art", "desert-bosses")
PREVIEWS = os.environ.get("BOSS_PREVIEWS", ART)
NAMES = {"wyvern": ("desert_wyvern", "Briarwing"), "drake": ("desert_drake", "Cindermaw"),
         "scorpion": ("desert_scorpion", "Pitchclaw")}

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
wanted = [a for a in argv if a in NAMES] or globals().get("BOSSES") or list(NAMES)
skip_export = "--preview-only" in argv

import bosskit
importlib.reload(bosskit)
results = {}
for key in wanted:
    module_name, boss_name = NAMES[key]
    try:
        mod = importlib.import_module(module_name)
        importlib.reload(mod)
        b = mod.build()
        unweighted = b.build()
        mod.animate(b)
        b.bake_clips()
        folder = os.path.join(OUT, boss_name)
        if not skip_export:
            b.export(os.path.join(folder, boss_name + ".fbx"), os.path.join(ART, boss_name + ".blend"),
                     os.path.join(folder, boss_name + "Clips.json"))
        renders = []
        if not ("--no-previews" in argv or globals().get("NO_PREVIEWS")):
            renders = mod.shots(b, os.path.join(PREVIEWS, key))
            bosskit.contact_sheet(renders, os.path.join(PREVIEWS, key + "_sheet.png"))
        results[key] = dict(b.stats(), unweighted=unweighted, renders=len(renders))
    except Exception:
        results[key] = {"error": traceback.format_exc()}
print("BOSSES_RESULT", results)
log = os.environ.get("BOSS_LOG")
if log:
    import json
    with open(log, "w") as f:
        json.dump(results, f, indent=1)
