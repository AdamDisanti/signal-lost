# Builds the Crystal Caves bosses in Blender, the same way desert_bosses.py builds the desert ones: model,
# skeleton, animation clips, the FBX for Unity, the clip timing table, the .blend and preview renders.
#   blender --background --python tools/blender/cave_bosses.py -- mole beetle
# Set SIGNAL_LOST_REPO to the repo root if it isn't the default below.
import sys, os, importlib, traceback

REPO = os.environ.get("SIGNAL_LOST_REPO", os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))
HERE = os.path.join(REPO, "tools", "blender")
if HERE not in sys.path:
    sys.path.insert(0, HERE)

OUT = os.path.join(REPO, "signal-lost", "Assets", "Prefabs", "Enemies", "Caves", "Bosses")
ART = os.path.join(REPO, "art", "cave-bosses")
PREVIEWS = os.environ.get("BOSS_PREVIEWS", ART)
NAMES = {"mole": ("cave_shard_mole", "ShardMole"), "beetle": ("cave_beetle", "CaveBeetle")}

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
wanted = [a for a in argv if a in NAMES] or globals().get("BOSSES") or list(NAMES)

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
        if "--preview-only" not in argv:
            b.export(os.path.join(folder, boss_name + ".fbx"), os.path.join(ART, boss_name + ".blend"),
                     os.path.join(folder, boss_name + "Clips.json"))
        renders = []
        if "--no-previews" not in argv:
            renders = mod.shots(b, os.path.join(PREVIEWS, key))
            bosskit.contact_sheet(renders, os.path.join(PREVIEWS, key + "_sheet.png"))
        results[key] = dict(b.stats(), unweighted=unweighted, renders=len(renders))
    except Exception:
        results[key] = {"error": traceback.format_exc()}
print("BOSSES_RESULT", results)
