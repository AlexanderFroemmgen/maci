from time import time
import sys
import json
import parameters

startTime = 0
measurements = []
messages = []

def _timeInMilliseconds():
	return int(round(time() * 1000))

def _offsetFromStart():
	return _timeInMilliseconds() - startTime

def start():
	global startTime, measurements
	startTime = _timeInMilliseconds()
	measurements = []
	parameters.requestedParams.add("simId")
	parameters.requestedParams.add("simInstanceId")

def param(key, default = None):
	if key not in parameters.params:
		if default is not None:
			return default
		else:
			print 'Requested parameter %s is undefined. Aborting experiment...' % key
			sys.exit(1) 
	parameters.requestedParams.add(key)
	return parameters.params[key]

def log(key, message):
	messages.append({'key': str(key), 'offset': _offsetFromStart(), 'type': 0, 'message': str(message)})

def addLogfile(filename):
    try:
        with open(filename, 'r') as myfile:
            log(filename, myfile.read())
    except IOError:
        warn(filename, "IO Error while adding logfile with MACI.")

def warn(key, message):
	messages.append({'key': str(key), 'offset': _offsetFromStart(), 'type': 1, 'message': str(message)})

def is_number(s):
    try:
        float(s)
        return True
    except ValueError:
        return False

def record(key, value, offset=None, key1=None, key2=None):
	if offset is not None:
		offset = int(offset)
	else:
		offset = _offsetFromStart()
	# value = value.strip()
	if not is_number(value):
		warn("record", "can not add " + str(value) + " for records")
		# breaks current work of Nikolas, but activate soon
		#return

	measurements.append({'key': str(key), 'offset': offset, 'value': str(value), 'key1': str(key1), 'key2': str(key2)})

def checkRequestedParams():
	for (paramKey, paramValue) in [(k, v) for (k, v) in parameters.params.iteritems() if k not in parameters.requestedParams]:
		warn("Framework", "Parameter " + str(paramKey) + " with value " + str(paramValue) + " was not requested by experiment")

def loadTmpResults():
        try:    
                with open('result_tmp.json', 'r') as file:
                        for line in file:
                                measurements.append(json.loads(line))
        except IOError:
                pass

def loadTmpMessages():
        try:    
                with open('message_tmp.json', 'r') as file:
                        for line in file:
                                messages.append(json.loads(line))
        except IOError:
                pass

def stop():
	checkRequestedParams()
	loadTmpResults()
	loadTmpMessages()

	with open('result.json', 'w') as file:
		json.dump(measurements, file)

	with open('messages.json', 'w') as file:
		json.dump(messages, file)
