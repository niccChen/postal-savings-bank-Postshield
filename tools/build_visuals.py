"""Rebuild documentation SVGs and the walkthrough GIF from captured app states."""
from pathlib import Path
from html import escape
import base64

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "docs/assets"
FONT = "Arial, Helvetica, PingFang SC, sans-serif"


def label(x, y, value, size=16, fill="#214b3e", weight="400", extra=""):
    return f'<text x="{x}" y="{y}" font-family="{FONT}" font-size="{size}" fill="{fill}" font-weight="{weight}" {extra}>{escape(value)}</text>'


def svg_file(name, width, height, content, title):
    (ASSETS / name).write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img"><title>{escape(title)}</title>{content}</svg>\n'
    )


wordmark = base64.b64encode((ASSETS / "psbc-wordmark.png").read_bytes()).decode()
hero = '<rect x=".5" y=".5" width="1199" height="299" rx="16" fill="#f7fbf8" stroke="#dce9e1"/>'
hero += '<path d="M16 0H1184Q1200 0 1200 16V6H0Q0 0 16 0" fill="#147755"/>'
hero += '<rect x="31" y="19" width="270" height="53" rx="5" fill="#ffffff"/>'
hero += f'<image x="37" y="24" width="259" height="41" xlink:href="data:image/png;base64,{wordmark}"/>'
hero += '<path d="M321 28V64" stroke="#c7dacf"/>'
hero += label(341, 43, "INTERNSHIP PROJECT", 12, "#47715f", "600", 'letter-spacing="1.5"')
hero += label(341, 62, "Postal Savings Bank of China", 13, "#58776c")
hero += label(39, 138, "PostShield", 59, "#164b39", "700")
hero += label(41, 177, "Spreadsheet data masking", 26, "#355e50")
hero += label(41, 216, "Configurable rules. Batch Excel processing.", 17, "#5a746a")
hero += label(41, 242, "A desktop workflow with backup and undo.", 17, "#5a746a")
hero += label(41, 276, "C# APPLICATION DEVELOPMENT", 11, "#4f7b69", "600", 'letter-spacing="1.5"')
hero += '<rect x="766" y="97" width="394" height="163" rx="11" fill="#ffffff" stroke="#d8e7de"/>'
hero += label(792, 126, "ONE RULE, CONSISTENT OUTPUT", 11, "#6d8b7d", "600", 'letter-spacing="1"')
hero += label(792, 159, "13800001234", 24, "#4d695d", "400", 'font-variant="tabular-nums"')
hero += '<path d="M795 183H828M821 177L828 183 821 189" fill="none" stroke="#8caf9e" stroke-width="2"/>'
hero += '<rect x="841" y="168" width="281" height="48" rx="7" fill="#edf6ef"/>'
hero += label(858, 199, "138****1234", 26, "#17734f", "600")
hero += label(792, 241, "Synthetic example · phone masking", 12, "#6c8177")
svg_file("postshield-header.svg", 1200, 300, hero, "PostShield — an internship project at Postal Savings Bank of China")

for filename, text, width in [("csharp.svg", "C#", 48), ("dotnet.svg", ".NET 8", 76), ("avalonia.svg", "Avalonia 11", 105), ("excel.svg", "Excel .xlsx", 97)]:
    content=f'<rect x=".5" y=".5" width="{width-1}" height="25" rx="5" fill="#edf6ef" stroke="#d0e3d7"/>'
    content+=label(width/2, 17, text, 12, "#356651", "600", 'text-anchor="middle"')
    svg_file(filename,width,26,content,text)

comparison='<rect x=".5" y=".5" width="1199" height="233" rx="12" fill="#ffffff" stroke="#dbe8e0"/>'
comparison+=label(28,34,"Inside the workbook",21,"#214b3e","600")
comparison+=label(28,58,"Verified output from demo-contacts-a.xlsx · synthetic records",13,"#6b8378")
for offset,title,fill in [(0,"BEFORE MASKING","#f3f6f4"),(601,"AFTER MASKING","#edf6ef")]:
    comparison+=f'<rect x="{24+offset}" y="77" width="551" height="119" rx="7" fill="{fill}"/>'
    comparison+=label(40+offset,100,title,11,"#4f7764","600",'letter-spacing="1"')
    for x,value in [(40,"Record"),(211,"Name"),(359,"Mobile")]:comparison+=label(x+offset,126,value,12,"#70857a")
    for y,record,name,phone in [(153,"DEMO-A01","张示例","13800001234"),(179,"DEMO-A02","李示例","13900005678")]:
        if offset:
            name=name[0]+"**"
            phone=phone[:3]+"****"+phone[-4:]
        comparison+=label(40+offset,y,record,15)
        comparison+=label(211+offset,y,name,16)
        comparison+=label(359+offset,y,phone,17,"#176d4d" if offset else "#36584a","600" if offset else "400")
comparison+=label(28,220,"Record IDs and region values are preserved. Undo restores the original workbook.",13,"#6b8378")
svg_file("masking-example.svg",1200,234,comparison,"Verified before-and-after example using generated data")

names=["02-scan","03-search","04-masked","05-undo"]
frames=[Image.open(ASSETS/"demo"/f"{name}.png").convert("RGB") for name in names]
assert len({im.size for im in frames}) == 1
frames[0].save(ASSETS/"walkthrough-static.png",optimize=True)
palette=frames[0].quantize(colors=192,method=Image.Quantize.MEDIANCUT)
indexed=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in frames]
indexed[0].save(ASSETS/"walkthrough.gif",save_all=True,append_images=indexed[1:],duration=[2400,2200,2800,2800],loop=0,optimize=True,disposal=1)
print(f"Built header, four labels, masking example, and {(ASSETS/'walkthrough.gif').stat().st_size:,}-byte walkthrough.")
