#!/bin/sh
set -eu
cd "$(dirname "$0")"

os="$(uname -s)"
arch="$(uname -m)"

case "$os-$arch" in
  Linux-x86_64)  rid=linux-x64;   lib=libijc_native.so;    flags="-shared -fPIC" ;;
  Linux-aarch64) rid=linux-arm64; lib=libijc_native.so;    flags="-shared -fPIC" ;;
  Darwin-arm64)  rid=osx-arm64;   lib=libijc_native.dylib; flags="-dynamiclib" ;;
  Darwin-x86_64) rid=osx-x64;     lib=libijc_native.dylib; flags="-dynamiclib" ;;
  *) echo "Unsupported platform $os-$arch. On Windows use native/build.cmd." >&2; exit 1 ;;
esac

mkdir -p "runtimes/$rid"

cc -std=c99 -O2 -Wall -Wextra -Werror -fvisibility=hidden $flags -o "runtimes/$rid/$lib" ijc_native.c
echo "built native/runtimes/$rid/$lib"

cc -std=c99 -Wall -Wextra -Werror -o test_native ijc_native.c test_native.c
./test_native
rm -f test_native
