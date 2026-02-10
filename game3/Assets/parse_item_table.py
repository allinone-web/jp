#!/usr/bin/env python3
"""Read tab-separated table from stdin: col0=$id, col1=name. Output desc lines for desc.txt."""
import sys
for line in sys.stdin:
    line = line.strip()
    if not line:
        continue
    parts = line.split('\t')
    if len(parts) < 2:
        continue
    key = parts[0].strip()
    name = parts[1].strip()
    if not key.startswith('$'):
        continue
    if ' ' in key:
        key = key.split()[0]
    print('desc\t' + key + '\t' + name + '\t' + name)
