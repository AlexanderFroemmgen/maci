package maci.loadtest;

import java.io.IOException;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.LinkedList;
import java.util.List;
import java.util.Random;

import maci.api.MACI;
import maci.api.Parameter;
import maci.api.ParameterAssignment;

public class LoadTest {

	public static void main(String[] args) throws IOException {
		final String ip = "10.0.254.21";
		//final String ip = "localhost";
		final String script = "...";
		final int numberOfInstancesTarget = 5;

		try (final PrintWriter pw = new PrintWriter("timePerSimulationInstance_" + ip + "_" + numberOfInstancesTarget
				+ "_" + new Random().nextInt(1000) + ".txt")) {

			final MACI maci = new MACI(ip + ":63658");
			final String simulationId = generateExperimentStub(script, maci);
			final List<String> simulationInstanceIds = new ArrayList<>();

			final long beginTime = System.currentTimeMillis();

			for (int i = 0; i < numberOfInstancesTarget; i++) {
				if (i % 1000 == 0) {
					System.out.printf("Now running instance number %8d after %10d seconds\n", i,
							(System.currentTimeMillis() - beginTime) / 1000);
				}

				long currentTime = System.currentTimeMillis();

				simulationInstanceIds.add(generateExperimentInstance(maci, simulationId));

				long duration = System.currentTimeMillis() - currentTime;
				pw.printf("%8d  %10d ms\n", i, duration);
			}
		}
	}

	private static String generateExperimentInstance(final MACI maci, final String simulationId) throws IOException {
		List<ParameterAssignment> parameterAssignments = new ArrayList<>();
		parameterAssignments.add(new ParameterAssignment("a", "5"));
		parameterAssignments.add(new ParameterAssignment("b", "6"));
		return maci.createExperimentInstance(simulationId, parameterAssignments, false);
	}

	private static String generateExperimentStub(final String script, final MACI maci) throws IOException {
		final List<Parameter> parameters = new ArrayList<>();
		parameters.add(new Parameter("a", Parameter.ParameterType.Int));
		parameters.add(new Parameter("b", Parameter.ParameterType.Int));
		return maci.addExperimentStub(script, "LoadTest", parameters, new LinkedList<String>(), "LoadTest");
	}
}
