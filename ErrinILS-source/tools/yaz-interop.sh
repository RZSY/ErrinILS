#!/bin/bash
# Optional: checks ErrinILS's Z39.50 client and server against YAZ, the reference implementation most library servers use.
#   sudo apt-get install yaz mono-mcs mono-devel      then:   bash tools/yaz-interop.sh
set -e
cd "$(dirname "$0")/.."
R=/usr/lib/mono/4.0-api; F="-nostdlib -lib:$R -r:mscorlib.dll,System.dll,System.Core.dll"
CORE="src/Json.cs src/Models.cs src/Marc.cs src/Z3950.cs"
mkdir -p build
mcs $F -out:build/zclient.exe $CORE tools/ZClient.cs; mcs $F -out:build/zserver.exe $CORE tools/ZServer.cs
echo "== ErrinILS client -> yaz-ztest =="
(yaz-ztest tcp:@:9999 >/dev/null 2>&1 & echo $! > build/ztest.pid); sleep 1
mono build/zclient.exe 127.0.0.1 9999 Default title computer | head -3
kill $(cat build/ztest.pid)
echo "== yaz-client -> ErrinILS server =="
(mono build/zserver.exe >/dev/null 2>&1 & echo $! > build/zsrv.pid); sleep 2
printf 'open 127.0.0.1:24999/errin\nfind @attr 1=7 9780064400558\nshow 1\nquit\n' | yaz-client | grep -E "Number of hits|245"
kill $(cat build/zsrv.pid) 2>/dev/null || true
