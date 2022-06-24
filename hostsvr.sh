hostaddr=$(ip a s eth0 | egrep -o 'inet [0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}' | cut -d' ' -f2)
./bld/legosvr --level /home/shanem/lego/testlvl --address $hostaddr
