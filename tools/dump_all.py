"""Dump all IL2CPP interop type members of an assembly to a text file for grepping."""
import sys
from dnfile import dnPE


def main():
    src = sys.argv[1]
    dst = sys.argv[2]
    pe = dnPE(src)
    with open(dst, "w", encoding="utf-8") as f:
        for t in pe.net.mdtables.TypeDef.rows:
            ns = str(t.TypeNamespace or "")
            name = str(t.TypeName or "")
            f.write("=== %s%s%s ===\n" % (ns, "." if ns else "", name))
            for idx in t.FieldList:
                if idx.row is not None:
                    f.write("  [F] %s\n" % idx.row.Name)
            for idx in t.MethodList:
                if idx.row is not None:
                    f.write("  [M] %s\n" % idx.row.Name)
    print("dumped", dst)


if __name__ == "__main__":
    main()
