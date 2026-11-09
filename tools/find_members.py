"""Find which type owns members matching keywords in a dump_all.py output file."""
import re
import sys


def main():
    dump = sys.argv[1]
    patterns = [re.compile(p, re.I) for p in sys.argv[2:]]
    current = "?"
    hits = {}
    with open(dump, encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("=== "):
                current = line[4:-4]
                continue
            m = line.strip()
            if not m:
                continue
            if any(p.search(m) for p in patterns):
                hits.setdefault(current, []).append(m)
    for t, members in hits.items():
        print("=== %s ===" % t)
        seen = set()
        for m in members:
            key = m.replace("NativeFieldInfoPtr_", "F:").replace("NativeMethodInfoPtr_", "M:")
            # only print real fields/methods, not the Native* stubs, to keep output short
            if key.startswith("F:") or key.startswith("M:") or m.startswith("["):
                if m in seen:
                    continue
            if m.startswith("  [") is False and ("Native" in m):
                continue
            seen.add(m)
            print("   ", m)
    print("\n# %d types" % len(hits))


if __name__ == "__main__":
    main()
