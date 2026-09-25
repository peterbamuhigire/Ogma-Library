"""Kaizen 2026-09-25: generate the synthetic Ogma test library (16 generated files + 1 copied golden scan = 17; original content only).

Usage: python New-SyntheticCorpus.py [output_dir]   (default: %TEMP%/ogma-corpus)
Requires: pip install pymupdf pypdf. Copy tests/golden-corpus/ocr-pipeline/scanned-image-only.pdf
into "<output_dir>/Edge Cases" to reproduce the audit corpus exactly. Phase 01 replaces this prototype.
"""
import os, sys, shutil, random, tempfile, fitz
from pypdf import PdfReader, PdfWriter
root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(tempfile.gettempdir(), "ogma-corpus")
for sub in ("Science", "History", "Fiction", "Edge Cases"):
    os.makedirs(os.path.join(root, sub), exist_ok=True)
books = [
 ("Science","The Physics of Everyday Light.pdf","The Physics of Everyday Light","Amara Okello","978-0-306-40615-7","optics photons refraction rainbow prism wavelength", 40),
 ("Science","Introduction to Tropical Ecology.pdf","Introduction to Tropical Ecology","Joseph Mugisha","978-1-86197-876-9","rainforest biodiversity canopy savanna ecosystems Lake Victoria", 60),
 ("Science","Algorithms Explained.pdf","Algorithms Explained","Grace Namutebi","978-0-13-110362-7","sorting graphs dynamic programming complexity recursion binary search", 120),
 ("History","A Short History of the Great Lakes Kingdoms.pdf","A Short History of the Great Lakes Kingdoms","Peter Byaruhanga","978-9970-02-123-4","Buganda Bunyoro Kitara Ankole Toro Chwezi dynasty oral tradition", 80),
 ("History","Trade Routes of East Africa.pdf","Trade Routes of East Africa","Sarah Achieng","","caravan ivory Zanzibar Mombasa monsoon dhow Swahili coast", 30),
 ("Fiction","The Lantern Keeper.pdf","The Lantern Keeper","Daniel Ssempa","978-3-16-148410-0","amber library lantern midnight keeper story chapter", 25),
 ("Fiction","Ngũgĩ-style Stories — Unicode Title.pdf","Hadithi za Jioni: Évening Tales","Wanjirũ Kamau","","hadithi jioni stories evening village fire", 12),
]
def make(folder,fname,title,author,isbn,words,pages,meta=True):
    doc=fitz.open()
    toc=[]
    for p in range(pages):
        page=doc.new_page()
        if p==0:
            page.insert_text((72,120),title,fontsize=22); page.insert_text((72,160),"by "+author,fontsize=14)
        elif p==1:
            page.insert_text((72,100),"Copyright page",fontsize=12)
            if isbn: page.insert_text((72,130),"ISBN "+isbn,fontsize=11)
            page.insert_text((72,160),"Synthetic test fixture. Original content for Ogma testing.",fontsize=10)
        else:
            if p%10==2:
                toc.append([1,f"Chapter {p//10+1}",p+1]); page.insert_text((72,90),f"Chapter {p//10+1}",fontsize=18)
            y=130
            for line in range(25):
                w=random.sample(words.split(),3)
                page.insert_text((72,y),f"Page {p+1} line {line}: the {w[0]} and the {w[1]} relate to {w[2]}.",fontsize=10); y+=22
    if toc: doc.set_toc(toc)
    if meta: doc.set_metadata({"title":title,"author":author,"subject":words.split()[0]})
    path=os.path.join(root,folder,fname); doc.save(path); return path
random.seed(7)
for b in books: make(*b)
# no metadata, filename-only
make("Edge Cases","untitled_scan_0042.pdf","","Unknown","","mystery unknown anonymous notes",5,meta=False)
# duplicate copy (same bytes) in another folder
shutil.copy(os.path.join(root,"Fiction","The Lantern Keeper.pdf"), os.path.join(root,"Edge Cases","The Lantern Keeper (copy).pdf"))
# encrypted
src=make("Edge Cases","_tmp.pdf","Locked Ledger","Treasurer","","ledger accounts balance treasury",4)
r=PdfReader(src); w=PdfWriter()
for pg in r.pages: w.add_page(pg)
w.encrypt("secret"); w.write(os.path.join(root,"Edge Cases","Locked Ledger (password secret).pdf")); os.remove(src)
# corrupt / truncated
data=open(os.path.join(root,"Science","Algorithms Explained.pdf"),"rb").read()
open(os.path.join(root,"Edge Cases","Truncated Download.pdf"),"wb").write(data[:len(data)//3])
open(os.path.join(root,"Edge Cases","not-really-a.pdf"),"wb").write(b"<html>this is not a pdf</html>")
open(os.path.join(root,"Edge Cases","empty.pdf"),"wb").write(b"")
# image-only scanned
pix=fitz.open(); pg=pix.new_page(); pg.insert_text((72,200),"Scanned image only page: amber library lantern",fontsize=20)
img=pg.get_pixmap(dpi=100); sc=fitz.open(); p2=sc.new_page(); p2.insert_image(p2.rect,pixmap=img); sc.save(os.path.join(root,"Edge Cases","Scanned Pamphlet (image only).pdf"))
# large
make("Science","Big Reference Handbook.pdf","Big Reference Handbook","Various","978-0-19-852663-6","reference handbook tables constants units appendix",900)
# deep nested path
deep=os.path.join(root,"Edge Cases","a"*40,"b"*40,"c"*40); os.makedirs(deep,exist_ok=True)
make(os.path.relpath(deep,root),"Deeply Nested.pdf","Deeply Nested","Nester","","nested folder depth",3)
print("ok")
