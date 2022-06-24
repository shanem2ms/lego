apt update
apt install cmake build-essential
apt-get install pkg-config
apt-get install curl zip unzip tar

git clone https://github.com/microsoft/vcpkg.git
cd vcpkg/
./bootstrap-vcpkg.sh 
./vcpkg install zlib fmt enet libzip curl mongo-cxx-driver cxxopts nlohmann-json
cd ~
mkdir lego
cd lego/
mkdir bld
ln -s /mnt/c/homep4/lego lego
cmake -DVCPKG_DIR=/home/shanem/vcpkg -DVCPKG_INSTALL_PATH=/home/shanem/vcpkg/installed/x64-linux ../lego -DBLOCKO_GAME=FALSE