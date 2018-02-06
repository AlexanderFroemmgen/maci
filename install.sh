# See also: https://www.microsoft.com/net/core#linuxubuntu
source /etc/os-release
sudo apt update || exit
# Setup general installation infrastructure
sudo apt install --yes wget curl apt-transport-https || exit
curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg
sudo mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg
sudo sh -c "echo \"deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-${UBUNTU_CODENAME}-prod $UBUNTU_CODENAME main\" > /etc/apt/sources.list.d/dotnetdev.list"

sudo apt update
sudo apt install --yes dotnet-sdk-2.0.0 npm nodejs-legacy python-dev screen || exit
sudo npm install gulp -g

# sudo apt install --yes python-pip
wget https://bootstrap.pypa.io/get-pip.py || exit
sudo python get-pip.py || exit
rm ./get-pip.py
pip install --upgrade pip || exit
# pip writes into the home direcotry. -H sets $HOME to the home directory of the target user
sudo -H pip install subprocess32 || exit
sudo -H pip install pandas || exit
sudo -H pip install matplotlib || exit
sudo -H pip install scipy || exit
sudo -H pip install scikit-learn || exit
sudo -H pip install jupyter || exit
jupyter nbextension enable --py widgetsnbextension
