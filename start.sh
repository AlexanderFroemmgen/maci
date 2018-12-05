cd maci_backend
screen -S backend -d -m bash -c "./run.sh"

cd AppData/JupyterNotebook
screen -S jupyter -d -m bash -c "jupyter notebook"

echo "Backend and Jupyter services are being started...
Please open the MACI web interface in your browser: http://localhost:63658
Depending on your internet connection, this may take some time.

You might have to check the Jupyter output with 'screen -r -d jupyter' and follow the instructions to finish the Jupyter setup. To access Jupyter outside of localhost, you have to start jupyter with 'jupyter notebook --ip <SERVER_IP> --port 8888' and replace 'SERVER_IP' with the IP you want to server jupyter on.

For the impatient:
  * You can check the current progress by invoking 'screen -r -d backend' for the MACI backend and 'screen -r -d jupyter' for the Jupyter service.
  * You can escape a screen by pressing 'Ctrl-A' and then 'D'. (NOT! Ctrl-C, which terminates the process.)
  * You can prepare to start over by invoking './stop.sh'."
