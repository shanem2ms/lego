cdir=$(pwd)
mkdir bld
cd bld
rm -rf *
cmake .. -G Xcode -DCMAKE_TOOLCHAIN_FILE=../ios.toolchain.cmake -DPLATFORM=OS64 -DENABLE_ARC=FALSE
