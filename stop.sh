screen -X -S backend quit
screen -X -S jupyter quit

if [ $# -eq 1 ]
then
	for i in $(seq 1 $1); do
		echo "stop worker $i"
		screen -X -S "worker-$i" quit;
	done
fi
