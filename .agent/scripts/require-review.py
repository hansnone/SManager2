#!/usr/bin/env python3
import sys, json
print(json.dumps({
    "decision": "continue",
    "reason": "Ejecuta la skill critic-ux antes de considerar la tarea terminada."
}))