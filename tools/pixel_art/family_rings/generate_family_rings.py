"""Anéis de pulso das famílias novas (Vârcolac, Liaozhai, Nachtalb, Strigoi), 192x96 (projeção 2:1), paleta AAP-64.
Desenhados em 96x96 visto de cima, achatados para 2:1 e dobrados (linhas de 2 px). Uso: py generate_family_rings.py"""
import math
from PIL import Image, ImageDraw
H=lambda s:tuple(int(s[i:i+2],16) for i in (0,2,4))
BG=H("242234"); N=96
from pathlib import Path
OUT=Path(__file__).resolve().parents[3]/'assets/pixel_art/effects/family_rings'
def canvas(): return Image.new("RGBA",(N,N),(0,0,0,0))
def ring(d,c,r,w=1,dash=0):
    for a in range(0,720):
        t=a/720*math.tau
        if dash and (a//dash)%2: continue
        for k in range(w):
            x=N/2+math.cos(t)*(r-k); y=N/2+math.sin(t)*(r-k); d.point((round(x),round(y)),fill=c)
# Varcolac: anel marrom + 3 garras
v=canvas(); d=ImageDraw.Draw(v)
ring(d,H("71413b"),40,2); ring(d,H("bb7547"),38,1,6)
for off in (-10,0,10):
    d.line([(N/2-14+off,N/2-22),(N/2+4+off,N/2+22)],fill=H("dba463"),width=2)
    d.line([(N/2-13+off,N/2-22),(N/2+5+off,N/2+22)],fill=H("f4d29c"),width=1)
# Liaozhai: octogono bagua jade
l=canvas(); d=ImageDraw.Draw(l)
for rr,c in ((42,H("23674e")),(34,H("328464"))):
    pts=[(N/2+math.cos(math.tau*i/8+math.pi/8)*rr,N/2+math.sin(math.tau*i/8+math.pi/8)*rr) for i in range(8)]
    d.polygon(pts,outline=c)
for i in range(8):
    t=math.tau*i/8
    for k,ln in enumerate((7,7,7)):
        r0=24+k*3.5
        x=N/2+math.cos(t)*r0; y=N/2+math.sin(t)*r0
        px,py=-math.sin(t),math.cos(t)
        if (i+k)%3==0: d.line([(x-px*4,y-py*4),(x+px*4,y+py*4)],fill=H("5daf8d"))
        else:
            d.line([(x-px*4,y-py*4),(x-px*1.2,y-py*1.2)],fill=H("5daf8d")); d.line([(x+px*1.2,y+py*1.2),(x+px*4,y+py*4)],fill=H("5daf8d"))
ring(d,H("92dcba"),9,1)
# Nachtalb: lua crescente + bruma
n=canvas(); d=ImageDraw.Draw(n)
ring(d,H("143464"),42,2); ring(d,H("285cc4"),40,1,3)
d.ellipse([N/2-16,N/2-16,N/2+16,N/2+16],fill=H("849be4")); d.ellipse([N/2-8,N/2-20,N/2+24,N/2+12],fill=(0,0,0,0))
for a in range(0,360,18):
    t=math.radians(a); d.point((round(N/2+math.cos(t)*30),round(N/2+math.sin(t)*30)),fill=H("b9bffb"))
# Strigoi: aro de laminas de sombra
s=canvas(); d=ImageDraw.Draw(s)
ring(d,H("4a5462"),40,1)
for i in range(12):
    t=math.tau*i/12
    a=(N/2+math.cos(t)*30,N/2+math.sin(t)*30); b=(N/2+math.cos(t+0.12)*42,N/2+math.sin(t+0.12)*42); c=(N/2+math.cos(t-0.12)*42,N/2+math.sin(t-0.12)*42)
    d.polygon([a,b,c],fill=H("8b93af") if i%2 else H("6d758d"),outline=H("333941"))
ring(d,H("b3b9d1"),26,1,4)

OUT.mkdir(parents=True, exist_ok=True)
from PIL import ImageFilter
FILL = {"claws": H("71413b"), "bagua": H("23674e"), "moon": H("143464"), "shadow": H("4a5462")}
BRIGHT = {"claws": H("f4d29c"), "bagua": H("cdf7e2"), "moon": None, "shadow": H("dae0ea")}
for name, im in (("claws", v), ("bagua", l), ("moon", n), ("shadow", s)):
    flat = im.resize((N, N // 2), Image.NEAREST).resize((N * 2, N), Image.NEAREST)
    if BRIGHT[name]:  # engrossa 1 px e põe um realce claro por baixo do traço, para ler no piso
        alpha = flat.getchannel("A").filter(ImageFilter.MaxFilter(3))
        halo = Image.new("RGBA", flat.size, FILL[name] + (255,)); halo.putalpha(alpha)
        halo.alpha_composite(flat); flat = halo
    base = Image.new("RGBA", flat.size, (0, 0, 0, 0))
    ImageDraw.Draw(base).ellipse([6, 3, flat.width - 7, flat.height - 4], fill=FILL[name] + (70,))
    base.alpha_composite(flat)
    base.save(OUT / f"ring_{name}.png")
print("ok")
