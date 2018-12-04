#!/bin/bash

# TODO merge this file and the other bootstrap file
#cd /home/mininet
mkdir maci
cd maci

wget {{Backend}}/workers/script.py -O worker.py -o wget-log.txt

# install pip and the monotonic package

# wget https://bootstrap.pypa.io/get-pip.py -O get-pip.py
# python get-pip.py
# sudo apt-get install python-pip
# pip install monotonic
# pip install subprocess32

apt-get install --yes python-minimal python-subprocess32 python-monotonic

python -u worker.py --backend {{Backend}} --capabilities {{Capabilities}} --maxidletime {{MaxIdleTime}} 2>&1 | tee python-log.txt
