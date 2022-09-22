#!/bin/bash
#
# Copyright 2021 Google Inc. All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
set -e

commandline="'$*'"

#if [[ $commandline == *"--cpp-std c++0x"* ]]; then
#  TEST_CPP_FLAGS="--cpp-std c++0x"
#else
#  # --cpp-std is defined by flatc default settings.
#  TEST_CPP_FLAGS=
#fi

#TEST_CPP_FLAGS="--gen-compare --cpp-ptr-type flatbuffers::unique_ptr $TEST_CPP_FLAGS"
TEST_CS_FLAGS="--cs-gen-json-serializer"
#TEST_TS_FLAGS="--gen-name-strings"
TEST_BASE_FLAGS="--reflect-names --gen-mutable --gen-object-api"
#TEST_RUST_FLAGS="$TEST_BASE_FLAGS --gen-all --gen-name-strings"
TEST_NOINCL_FLAGS="$TEST_BASE_FLAGS --no-includes"


../bufc --binary --csharp \
$TEST_NOINCL_FLAGS $TEST_CPP_FLAGS $TEST_CS_FLAGS -I include_test monster_test.fbs monsterdata_test.json

../bufc --csharp \
$TEST_NOINCL_FLAGS $TEST_CPP_FLAGS $TEST_CS_FLAGS $TEST_TS_FLAGS -o namespace_test namespace_test/namespace_test1.fbs namespace_test/namespace_test2.fbs

../bufc --csharp $TEST_BASE_FLAGS $TEST_CPP_FLAGS $TEST_CS_FLAGS $TEST_TS_FLAGS -o union_vector ./union_vector/union_vector.fbs
../bufc $TEST_BASE_FLAGS $TEST_TS_FLAGS -b -I include_test monster_test.fbs unicode_test.json
../bufc -b --schema --bfbs-comments --bfbs-filenames . --bfbs-builtins -I include_test monster_test.fbs
../bufc -b --schema --bfbs-comments --bfbs-builtins -I include_test arrays_test.fbs
../bufc --jsonschema --schema -I include_test monster_test.fbs
../bufc --csharp $TEST_NOINCL_FLAGS $TEST_CPP_FLAGS $TEST_CS_FLAGS monster_extra.fbs monsterdata_extra.json
../bufc --csharp --jsonschema $TEST_NOINCL_FLAGS $TEST_CPP_FLAGS $TEST_CS_FLAGS --scoped-enums arrays_test.fbs

# Generate optional scalar code for tests.
../bufc --csharp --gen-object-api optional_scalars.fbs

# Generate the keywords tests
../bufc --csharp $TEST_BASE_FLAGS $TEST_CS_FLAGS -o keyword_test ./keyword_test.fbs

# Tests if the --filename-suffix and --filename-ext works and produces the same
# outputs.
../bufc --csharp --filename-suffix _suffix --filename-ext c# $TEST_NOINCL_FLAGS $TEST_CPP_FLAGS -I include_test monster_test.fbs
if [ -f "monster_test_suffix.c#" ]; then
  if ! cmp -s "monster_test_suffix.c#" "monster_test_generated.cs"; then
    echo "[Error] Filename suffix option did not produce identical results"
  fi
  rm "monster_test_suffix.c#"
else
  echo "[Error] Filename suffix option did not produce a file"
fi

cd ../samples
../bufc -b --schema --bfbs-comments --bfbs-builtins monster.fbs
