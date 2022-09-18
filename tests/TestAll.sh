cd ..
./flattests
cd tests

echo "************************ C#:"

cd FlatBuffers.Test
sh NetTest.sh
cd ..

echo "************************ C:"

echo "(in a different repo)"
