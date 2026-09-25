"""Deterministic synthetic PDF corpus for the Ogma Library real-window journeys (Sept-23 T01.4).

Usage:
    python tests/fixtures/corpus/New-SyntheticCorpus.py <output_dir> [--manifest <file>]

Requires pymupdf and pypdf (pip install pymupdf pypdf). All content is original synthetic text;
no real book content is ever generated or committed. The output is byte-for-byte reproducible:
every random source is seeded per file, document IDs and dates are fixed, and encryption uses a
fixed document ID, so two runs produce identical SHA-256 digests (checked by --manifest).

The corpus (17 files) matches the Sept-23 audit corpus:
  8 well-formed books with metadata, ISBNs (where valid) and TOCs, including a 900-page book
  and a Unicode filename; 1 untitled file (no metadata); 1 byte-identical duplicate;
  1 password-protected file; 1 truncated file; 1 non-PDF with a .pdf extension; 1 zero-byte
  file; 2 image-only PDFs; 1 deeply nested file.
The journey oracle is tests/fixtures/corpus/expected.json. Keep the two in step.
"""
import argparse
import hashlib
import json
import os
import random
import shutil
import sys

import fitz  # pymupdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ByteStringObject

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
GOLDEN_SCAN = os.path.join(REPO, "tests", "golden-corpus", "ocr-pipeline", "scanned-image-only.pdf")
FIXED_DATE = "D:20260925000000Z"

BOOKS = [
    ("Science", "The Physics of Everyday Light.pdf", "The Physics of Everyday Light", "Amara Okello",
     "978-0-306-40615-7", "optics photons refraction rainbow prism wavelength", 40),
    ("Science", "Introduction to Tropical Ecology.pdf", "Introduction to Tropical Ecology", "Joseph Mugisha",
     "978-1-86197-876-9", "rainforest biodiversity canopy savanna ecosystems Lake Victoria", 60),
    ("Science", "Algorithms Explained.pdf", "Algorithms Explained", "Grace Namutebi",
     "978-0-13-110362-7", "sorting graphs dynamic programming complexity recursion binary search", 120),
    ("History", "A Short History of the Great Lakes Kingdoms.pdf", "A Short History of the Great Lakes Kingdoms",
     "Peter Byaruhanga", "978-9970-02-123-4", "Buganda Bunyoro Kitara Ankole Toro Chwezi dynasty oral tradition", 80),
    ("History", "Trade Routes of East Africa.pdf", "Trade Routes of East Africa", "Sarah Achieng",
     "", "caravan ivory Zanzibar Mombasa monsoon dhow Swahili coast", 30),
    ("Fiction", "The Lantern Keeper.pdf", "The Lantern Keeper", "Daniel Ssempa",
     "978-3-16-148410-0", "amber library lantern midnight keeper story chapter", 25),
    ("Fiction", "Ngũgĩ-style Stories — Unicode Title.pdf", "Hadithi za Jioni: Évening Tales", "Wanjirũ Kamau",
     "", "hadithi jioni stories evening village fire", 12),
]


def save(doc, path):
    doc.set_metadata({**(doc.metadata or {}), "creationDate": FIXED_DATE, "modDate": FIXED_DATE,
                      "producer": "Ogma synthetic corpus", "creator": "Ogma synthetic corpus"})
    doc.save(path, garbage=4, deflate=True, no_new_id=True)


def make(root, folder, fname, title, author, isbn, words, pages, meta=True):
    rng = random.Random(f"{folder}/{fname}")
    doc = fitz.open()
    toc = []
    for p in range(pages):
        page = doc.new_page()
        if p == 0:
            if title:
                page.insert_text((72, 120), title, fontsize=22)
            page.insert_text((72, 160), "by " + author, fontsize=14)
        elif p == 1:
            page.insert_text((72, 100), "Copyright page", fontsize=12)
            if isbn:
                page.insert_text((72, 130), "ISBN " + isbn, fontsize=11)
            page.insert_text((72, 160), "Synthetic test fixture. Original content for Ogma testing.", fontsize=10)
        else:
            if p % 10 == 2:
                toc.append([1, f"Chapter {p // 10 + 1}", p + 1])
                page.insert_text((72, 90), f"Chapter {p // 10 + 1}", fontsize=18)
            y = 130
            for line in range(25):
                w = rng.sample(words.split(), 3)
                page.insert_text((72, y), f"Page {p + 1} line {line}: the {w[0]} and the {w[1]} relate to {w[2]}.",
                                 fontsize=10)
                y += 22
    if toc:
        doc.set_toc(toc)
    if meta:
        doc.set_metadata({"title": title, "author": author, "subject": words.split()[0]})
    path = os.path.join(root, folder, fname)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    save(doc, path)
    return path


def generate(root):
    for sub in ("Science", "History", "Fiction", "Edge Cases"):
        os.makedirs(os.path.join(root, sub), exist_ok=True)
    for b in BOOKS:
        make(root, *b)
    make(root, "Edge Cases", "untitled_scan_0042.pdf", "", "Unknown", "", "mystery unknown anonymous notes", 5,
         meta=False)
    shutil.copyfile(os.path.join(root, "Fiction", "The Lantern Keeper.pdf"),
                    os.path.join(root, "Edge Cases", "The Lantern Keeper (copy).pdf"))
    # Password-protected (user password "secret"); fixed ID keeps the RC4 key and bytes stable.
    src = make(root, "Edge Cases", "_tmp.pdf", "Locked Ledger", "Treasurer", "", "ledger accounts balance treasury", 4)
    reader = PdfReader(src)
    writer = PdfWriter()
    for pg in reader.pages:
        writer.add_page(pg)
    fixed_id = ByteStringObject(hashlib.md5(b"ogma-locked-ledger").digest())
    writer._ID = [fixed_id, fixed_id]  # noqa: SLF001 (pypdf has no public API for a fixed ID)
    writer.encrypt("secret", algorithm="RC4-128")
    with open(os.path.join(root, "Edge Cases", "Locked Ledger (password secret).pdf"), "wb") as fh:
        writer.write(fh)
    os.remove(src)
    with open(os.path.join(root, "Science", "Algorithms Explained.pdf"), "rb") as fh:
        data = fh.read()
    with open(os.path.join(root, "Edge Cases", "Truncated Download.pdf"), "wb") as fh:
        fh.write(data[: len(data) // 3])
    with open(os.path.join(root, "Edge Cases", "not-really-a.pdf"), "wb") as fh:
        fh.write(b"<html>this is not a pdf</html>")
    open(os.path.join(root, "Edge Cases", "empty.pdf"), "wb").close()
    # Image-only PDFs: one generated here, one copied from the OCR golden corpus.
    pix_doc = fitz.open()
    pg = pix_doc.new_page()
    pg.insert_text((72, 200), "Scanned image only page: amber library lantern", fontsize=20)
    img = pg.get_pixmap(dpi=100)
    scan = fitz.open()
    p2 = scan.new_page()
    p2.insert_image(p2.rect, pixmap=img)
    save(scan, os.path.join(root, "Edge Cases", "Scanned Pamphlet (image only).pdf"))
    shutil.copyfile(GOLDEN_SCAN, os.path.join(root, "Edge Cases", "Scanned Handout (golden).pdf"))
    make(root, "Science", "Big Reference Handbook.pdf", "Big Reference Handbook", "Various",
         "978-0-19-852663-6", "reference handbook tables constants units appendix", 900)
    deep = os.path.join("Edge Cases", "a" * 40, "b" * 40, "c" * 40)
    make(root, deep, "Deeply Nested.pdf", "Deeply Nested", "Nester", "", "nested folder depth", 3)


def manifest(root):
    rows = {}
    for dirpath, _, files in os.walk(root):
        for f in files:
            full = os.path.join(dirpath, f)
            rel = os.path.relpath(full, root).replace(os.sep, "/")
            with open(full, "rb") as fh:
                rows[rel] = hashlib.sha256(fh.read()).hexdigest()
    return dict(sorted(rows.items()))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("output")
    ap.add_argument("--manifest", help="write a JSON SHA-256 manifest of the generated files")
    args = ap.parse_args()
    if os.path.exists(args.output) and os.listdir(args.output):
        sys.exit(f"output directory is not empty: {args.output}")
    generate(args.output)
    rows = manifest(args.output)
    if args.manifest:
        with open(args.manifest, "w", encoding="utf-8") as fh:
            json.dump(rows, fh, indent=2, ensure_ascii=False)
    print(f"ok {len(rows)} files")


if __name__ == "__main__":
    main()
