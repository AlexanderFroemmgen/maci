### CFG int myParameter "Just an example"

import framework
import os

if __name__ == '__main__':
    """" 
	Welcome to MACI

	This file guides you through the creation of your first experiment study.

	1. Inform the framework about the beginning of the experiment
    """
    
    framework.start()

    """
        2. Configure the experiment according to the given environment and configuration parameters.
    """
    
    print "myParameter has the value", framework.param('myParameter')
    print "myParameter has the value", {{myParameter}}
    
    """
        3. Store measurement results during the experiment.
           You might even store multiple values for the same metric key.
    """
    
    framework.record("targetMetric", 1)

    """
        Each measurement record contains a time offset.
        Override this offset if required (e.g., for time-based analysis)
    """
    
    framework.record("targetMetric", 1, offset=5)

    """
        4. Store additional logs and warnings during the experiment.
        Note that the standard output is also recorded, e.g., prints.
    """
    
    framework.log("key", "value")
    framework.warn("key", "value")

    # create dummy logfile
    os.system("echo blub > mylogfile.txt")

    framework.addLogfile("mylogfile.txt")
    
    """
        5. Finally, inform the framework about the end of the experiment.
    """
    
    framework.stop()
