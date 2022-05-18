cdir=$(pwd)
mkdir bld
cd bld
rm -rf *
set -x 
cmake .. -G Xcode -DCMAKE_TOOLCHAIN_FILE=../ios.toolchain.cmake -DPLATFORM=OS64 -DENABLE_ARC=FALSE -DDEPLOYMENT_TARGET=15.0 -DVCPKG_INSTALL_PATH=$cdir/../vcpkg/installed/arm64-ios/
