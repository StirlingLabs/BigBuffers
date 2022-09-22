#!/bin/sh

#PROJ_FILE=FlatBuffers.Test.csproj
CORE_PROJ_FILE=../../net/BigBuffers.Tests/BigBuffers.Tests.csproj

TEMP_DOTNET_DIR=.dotnet_tmp
TEMP_BIN=.tmp

[ -d $TEMP_DOTNET_DIR ] || mkdir $TEMP_DOTNET_DIR

dotnet restore -r linux-x64 $CORE_PROJ_FILE

# Testing C# on Linux using .Net 6.
dotnet msbuild -property:Configuration=Release,OutputPath=$TEMP_BIN -verbosity:minimal $CORE_PROJ_FILE
$TEMP_BIN/FlatBuffers.Core.Test.exe
rm -rf $TEMP_BIN

rm -rf obj
