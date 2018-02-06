#!/usr/bin/env python2
# -*- coding: utf-8 -*-

from time import sleep, time
import httplib
import json
import sys
import argparse
import socket
from pprint import pprint
from zipfile import ZipFile
from StringIO import StringIO
from random import randint
import subprocess
import subprocess32 # has timeout backport
from subprocess32 import TimeoutExpired
import shutil
import hashlib
import os.path
import os
from threading import Thread

try:
	from monotonic import monotonic
except ImportError:
	print 'Module monotonic is not available. \'--maxidletime\' may not work properly if the system clock is changed.'
	def monotonic():
		return time()

# configuration
serverUri = 'localhost:63658'
capabilities = []
maxIdleTime = 3600 
maxSimTime = 60*10
clearTmpDir = False
numberOfWorker = 1

tokenForUri = {}

def getHeadersForUri(uri):
	global tokenForUri
	if uri in tokenForUri:
		return {'Content-Type':'application/json', 'Worker-Token': tokenForUri[uri]}
	return {'Content-Type':'application/json', 'Worker-Token': '?'}

class WorkerNotRegisteredException(Exception):
	pass

def configure():
	parser = argparse.ArgumentParser()
	parser.add_argument('--backend', default='localhost:63658')
	parser.add_argument('--second_backend', default='')
	parser.add_argument('--capabilities', default='', nargs='*')
	parser.add_argument('--maxidletime', default=3600, type=int)
	parser.add_argument('--maxsimtime', default=60*10, type=int)
	parser.add_argument("--no-clear-tmp-dir", dest='clear_tmp', action='store_false')
	parser.add_argument("--number-of-worker", dest='number_worker', default=1, type=int) 
	parser.set_defaults(clear_tmp=True)

	args = parser.parse_args()
	
	global serverUri, secondServerUri, capabilities, maxIdleTime, maxSimTime, executedInstallScripts, numberOfWorker
	serverUri = args.backend
	secondServerUri = args.second_backend
	capabilities = args.capabilities
	maxIdleTime = args.maxidletime
	maxSimTime = args.maxsimtime
	clearTmpDir = args.clear_tmp
	numberOfWorker = args.number_worker
	executedInstallScripts = {}

	print serverUri
	print secondServerUri
	print capabilities
	print maxIdleTime
	print maxSimTime

def registerSelfAsWorker(uri):
	global tokenForUri
	server = httplib.HTTPConnection(uri)
	registration_payload = json.dumps({'Capabilities': capabilities})
	server.request('POST', '/workers', registration_payload, getHeadersForUri(uri))
	response = server.getresponse()
	if response.status == 201:
		data = json.loads(response.read())
		token = data['Token']
			
		tokenForUri[uri] = token
		print 'Registered at server %s with token: %s' % (uri, token)
	else:
		print 'Could not register as worker!'
		pprint(response.read())

def findAndExecutePendingJob(uri):
	server = httplib.HTTPConnection(uri)
	server.request('GET', '/random_job', headers=getHeadersForUri(uri))
	response = server.getresponse()
	location = response.getheader('Location')
	if response.status == 401 or response.status == 403:
		raise WorkerNotRegisteredException
	elif response.status == 302 and location != None:
		print 'Pending job found at', location, 'for', uri
		executeJob(uri, location)
		return True
	else:
		print 'No pending experiment at this time for', uri
		return False

def installScriptAlreadyExecuted(script):
	global executedInstallScripts
	hex_dig = hashlib.sha256(script).hexdigest()
	if hex_dig in executedInstallScripts:
		return True
	else:
		executedInstallScripts[hex_dig] = hex_dig
		return False

def loadSimConfig(configFile):
	try:    
		with open(configFile, 'r') as file:
			cfg=json.loads(file.read())
			print "found config", cfg
			return cfg["timeout"] * 60
	except IOError:
		pass

def executeJob(uri, job_location):
	# download zip file with the experiment
	server = httplib.HTTPConnection(uri)
	server.request('GET', job_location + '/experiment.zip')
	response = server.getresponse()
	content = response.read()

	# unzip to new folder
	zipfile = ZipFile(StringIO(content))
	dirname = 'sim' + job_location.replace('/', '_')
	zipfile.extractall('./' + dirname)

	logfile = open('./%s/log.txt' % dirname, 'w')
	logAppend = ""
	result = 0

	maxSimTimeConfig = loadSimConfig('./%s/config.json' % dirname)
	if maxSimTimeConfig is None:
		print "No individuel timeout... Fallback to default of", maxSimTime
		maxSimTimeConfig = maxSimTime

	# execute installation script (if it exists and has not been run yet)
	if os.path.isfile('./%s/install.py' % dirname):
		installScript = open('./%s/install.py' % dirname, 'r').read()

		if installScriptAlreadyExecuted(installScript):
			print "Install script already executed"
		else:
			print 'Installing...'
			try:
				result = subprocess32.call('python install.py',
					shell=True,
					stdout=logfile,
					stderr=subprocess.STDOUT,
					timeout=maxSimTimeConfig,
					cwd='./%s' % dirname)
			except TimeoutExpired:
				print "Timeout expired"
				logAppend = "\nWorker Timeout Expired after " + str(maxSimTimeConfig) + "s"
				result = 1
			if result != 0:
				logAppend = "\nError during installation."
	
	# execute experiment
	if result == 0:
		print 'Executing...'
		try:
			result = subprocess32.call('python experiment.py',
				shell=True,
				stdout=logfile,
				stderr=subprocess.STDOUT,
				timeout=maxSimTimeConfig,
				cwd='./%s' % dirname)
		except TimeoutExpired:
			print "Timeout expired"
			logAppend = "\nWorker Timeout Expired after " + str(maxSimTimeConfig) + "s"
			result = 1

	log_content = open('./%s/log.txt' % dirname, 'r').read() + logAppend

	if result == 0:
		try:
			records = open('./%s/result.json' % dirname, 'r').read()
			messages = open('./%s/messages.json' % dirname, 'r').read()
		except:
			records = "[]"
			messages = "[]"
			log_content += "\n---\nresult.json does not exist. Did you forget to call framework.stop()?"

		print 'Job was executed successfully!'
		print 'Sending results to server...'
		server = httplib.HTTPConnection(uri)
		payload = json.dumps({'Log': log_content, 'Records': json.loads(records), 'LogMessages': json.loads(messages)})
		server.request('PUT', job_location + '/results', payload, getHeadersForUri(uri))
		response = server.getresponse()
		if response.status == 401 or response.status == 403:
			raise WorkerNotRegisteredException
		elif response.status == 200:
			print 'Results sent successfully!'
			
		else:
			print 'Results could not be sent. (%s)' % response.status
			print response.read()
	else:
		print 'An error occured in experiment %s' % job_location
		print log_content
		error_payload = json.dumps({'ErrorLog':log_content})
		server = httplib.HTTPConnection(uri)
		server.request('PUT', job_location + '/error', error_payload, getHeadersForUri(uri))
		response = server.getresponse()
		if response.status == 401 or response.status == 403:
			raise WorkerNotRegisteredException

	# clean up file system
	if os.name != "nt":
		shutil.rmtree(dirname)
		if clearTmpDir:
			subprocess.check_call("rm -r -f /tmp/*", shell=True)


def spawn_worker_instance(arg):
    os.system("python worker.py --backend " + str(serverUri) + " --capabilities " + str(capabilities) + " --maxidletime " + str(maxIdleTime) + " --maxsimtime " + str(maxSimTime))

if __name__ == '__main__':
	configure()
	
	if numberOfWorker > 1:
		print "Starting", numberOfWorker, "worker"
		threads = []
		for i in range(0, numberOfWorker):
			print "Starting worker", i
			thread = Thread(target = spawn_worker_instance, args = (10, ))
			thread.start()
			threads.append(thread)
		for thread in threads:
			thread.join()
		sys.exit(0)

	registerSelfAsWorker(serverUri)
	if secondServerUri != '':
		registerSelfAsWorker(secondServerUri)

	lastJobTime = monotonic()
	while True:
		print '---'
		print 'Max idle time: %s. Last job: %s, current time: %s. diff: %s' % (maxIdleTime, lastJobTime, monotonic(), monotonic() - lastJobTime)

		if (maxIdleTime != -1 and monotonic() - lastJobTime > maxIdleTime):
			print 'Max idle time (%s) has been exceeded. Last job: %s, current time: %s. Exiting...' % (maxIdleTime, lastJobTime, monotonic())
			sys.exit(1)

		try:
			jobWasFound = findAndExecutePendingJob(serverUri)

			if secondServerUri != '':
				jobWasFound = findAndExecutePendingJob(secondServerUri) or jobWasFound

			if jobWasFound:
				lastJobTime = monotonic()
				pass # find a new job immediately
			else:
				sleep(10) # wait 10 seconds to keep it interactive
		except WorkerNotRegisteredException:
			# backend has probably rebooted --> register again
			registerSelfAsWorker(serverUri)
		except socket.error as e:
			print 'Socket error occured. Maybe the server is not responding? (%s)' % repr(e)
			# backend is probably offline --> wait some time and try again
			sleep(120)
