# MACI Research

MACI is a framework for the management, scalable execution, and analysis of a large number of network experiments. You can find more information at the offical [maci-research.net page](https://maci-research.net).

> **Be careful** when running MACI, otherwise you will provide **remote code execution as a service** for your network.

## Installation and Startup

### Ubuntu

Run
```
./install.sh 
```
to install all required dependencies. 

Run 
```
./start.sh
```
to start MACI. If you experience problems, it sometimes helps to run the script as root, e.g., with
```
sudo ./install.sh
sudo ./start.sh
```

### Manual Installation

We do not provide an install script for Windows. You might want to manually execute the steps of the *install.sh* script. In general, having [dotnet core 2.0](https://www.microsoft.com/net/download/windows) should be sufficient for the backend.

## Getting Started

MACI is self-explaining and provides a web interface at http://<SERVER_IP>:63658 (e.g., [http://localhost:63658](http://localhost:63658)).
Check the web interface for more information.

To access Jupyter outside of localhost, start jupyter with
```
jupyter notebook --ip <SERVER_IP> --port 8888
```

## Contribute

We are happy for all kind of contributions, including bug fixes and additional features.

## Credits

Design and Concepts: [@AlexanderFroemmgen](https://github.com/AlexanderFroemmgen), [@a10r](https://github.com/a10r), [@dstohr](https://github.com/dstohr)

Implementation: [@AlexanderFroemmgen](https://github.com/AlexanderFroemmgen), [@a10r](https://github.com/a10r), [@max-weller](https://github.com/max-weller)

Fixes and Minor Features: [@RolandKluge](https://github.com/RolandKluge), [@ckleemann](https://github.com/ckleemann), [@Nikolasel](https://github.com/Nikolasel), [@martinpfannemueller](https://github.com/martinpfannemueller), [@dstohr](https://github.com/dstohr), [@rhaban](https://github.com/rhaban), [@IstEchtSo](https://github.com/IstEchtSo)
