import sys, os
sys.path.insert(0, "/mnt/project-files/sala-cripta/gerador"); os.chdir("/mnt/project-files/sala-cripta/gerador")
from PIL import Image
import wing2
V = Image.open(sys.argv[1]).convert("RGBA")
wing2.load_virgil = lambda: V
img = wing2.build(sys.argv[2])
w, h = img.size; img.resize((w*2, h*2), Image.NEAREST).save(sys.argv[2].replace(".png", "_x2.png"))
print(img.size)
