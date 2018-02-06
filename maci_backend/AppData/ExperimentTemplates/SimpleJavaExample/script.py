import framework
from subprocess import check_output

if __name__ == '__main__':
    framework.start()
    result = check_output("java -jar testJar.jar {{number_of_nodes}} {{avg_node_degree}}", shell=True)
    framework.log("java_call", result)
    framework.stop()
