#!/usr/bin/env python3
import sys, json

payload = json.load(sys.stdin)
tool = payload.get("toolCall", {})
args = tool.get("args", {})

# Ajusta los paths permitidos según tu plan
ALLOWED = ["src/", "tests/", "docs/"]

target = args.get("TargetFile") or args.get("path") or ""
if target and not any(target.startswith(p) for p in ALLOWED):
    print(json.dumps({
        "decision": "deny",
        "reason": f"Path no permitido: {target}. Solo se permiten: {ALLOWED}"
    }))
else:
    print(json.dumps({"decision": "allow"}))