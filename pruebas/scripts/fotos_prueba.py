import sys, os
from PIL import Image, ImageDraw
d = sys.argv[1]
def foto(nombre, w, h, color, texto, exif_orient=None, fmt="JPEG"):
    im = Image.new("RGB", (w, h), color)
    dr = ImageDraw.Draw(im)
    for i in range(0, w, w // 8): dr.line([(i, 0), (i, h)], fill=(255, 255, 255), width=3)
    dr.rectangle((w*0.05, h*0.05, w*0.45, h*0.30), fill=(255, 220, 0))   # marca arriba-izquierda
    dr.text((w*0.08, h*0.12), texto, fill=(0, 0, 0))
    dr.ellipse((w*0.6, h*0.55, w*0.9, h*0.9), fill=(255, 255, 255))
    kw = {}
    if exif_orient:
        ex = Image.Exif(); ex[0x0112] = exif_orient; kw["exif"] = ex.tobytes()
    im.save(os.path.join(d, nombre), fmt, **kw)
foto("a_paisaje.jpg", 3000, 2000, (40, 120, 200), "PAISAJE")
foto("b_retrato.jpg", 2000, 3000, (200, 80, 60), "RETRATO")
foto("c_exif6.jpg", 3000, 2000, (60, 160, 90), "EXIF 6", exif_orient=6)   # se ve vertical
foto("d_cuadrada.png", 1500, 1500, (150, 90, 180), "PNG", fmt="PNG")
