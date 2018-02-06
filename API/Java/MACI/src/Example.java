import java.io.IOException;
import java.util.ArrayList;
import java.util.LinkedList;
import java.util.List;

import maci.api.MACI;
import maci.api.Parameter;
import maci.api.ParameterAssignment;

/**
 * This example connects to a MACI instance at localhost:63658,
 * creates an experiment, creates and experiment instance, and 
 * retrieves the experiment results.
 */
public class Example {

	public static void main(String[] args) throws IOException {
		/* Start with creating a MACI instance */
		final MACI maci = new MACI("localhost:63658");
		
		/* Create a new experiment study */
		final String newExperimentId = generateExperimentStub(maci, "empty script");
		System.out.println("Now created new experiment with id " + newExperimentId);
		
		/* Add a new experiment instance */
		final String newExperimentInstanceId = generateExperimentInstance(maci, newExperimentId);
		System.out.println("Now created new experiment instance with id " + newExperimentInstanceId);

		/* Retrieve the state of the experiment instance */
		System.out.println(maci.getExperimentInstanceResult(newExperimentId, newExperimentInstanceId));
	}
	
	private static String generateExperimentStub(final MACI maci, final String script) throws IOException {
		final List<Parameter> parameters = new ArrayList<>();
		parameters.add(new Parameter("a", Parameter.ParameterType.Int));
		parameters.add(new Parameter("b", Parameter.ParameterType.Int));
		/* Important: Make sure that the experiment study template is created before */
		final String name = "SimplePythonExample"; 
		return maci.addExperimentStub(script, "Java API Example", parameters, new LinkedList<String>(), name);
	}
	
	private static String generateExperimentInstance(final MACI maci, final String experimentId) throws IOException {
		final List<ParameterAssignment> parameterAssignments = new ArrayList<>();
		parameterAssignments.add(new ParameterAssignment("a", "5"));
		parameterAssignments.add(new ParameterAssignment("b", "6"));
		return maci.createExperimentInstance(experimentId, parameterAssignments, false);
	}
}
