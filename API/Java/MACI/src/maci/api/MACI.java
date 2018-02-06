package maci.api;

import java.io.BufferedReader;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLConnection;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import com.google.gson.Gson;
import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

public class MACI {

	private final String experimentsBase;

	public MACI(final String server) {
		this.experimentsBase = String.format("http://%s/experiments/", server);
	}

	/**
	 * Creates an experiment instance and returns its identifier.
	 */
	public String createExperimentInstance(final String experimentId, final List<ParameterAssignment> parameterAssignments,
			final boolean rawJson) throws IOException {
		URLConnection connection = new URL(this.experimentsBase + experimentId + "/addCombination").openConnection();
		connection.setRequestProperty("Content-Type", "application/json");
		connection.setDoOutput(true);
		try (PrintWriter pw = new PrintWriter(connection.getOutputStream())) {
			pw.print("[");

			boolean firstOne = true;
			for (ParameterAssignment pa : parameterAssignments) {
				if (!firstOne)
					pw.print(", ");
				firstOne = false;

				pw.print(pa.toApiJson());
			}
			pw.println("]");
			pw.flush();

			BufferedReader br = new BufferedReader(new InputStreamReader(connection.getInputStream()));
			StringBuffer sb = new StringBuffer();
			String tmp = null;
			while ((tmp = br.readLine()) != null) {
				sb.append(tmp);
			}
			String json = sb.toString();

			if (rawJson) {
				return json;
			} else {
				return new Gson().fromJson(json, ExperimentInstance.class).getExperimentInstanceId();
			}
		}
	}

	public void deleteExperiment(final String experimentId) throws IOException {
		HttpURLConnection connection = (HttpURLConnection) new URL(this.experimentsBase + experimentId)
				.openConnection();
		connection.setRequestProperty("Content-Type", "application/x-www-form-urlencoded");
		connection.setDoOutput(true);
		connection.setRequestMethod("DELETE");
		connection.getInputStream();
	}

	/**
	 * Creates an empty experiment stub and returns the experimentId.
	 */
	public String addExperimentStub(final String script, final String name, final List<Parameter> parameters,
			final List<String> requiredCapabilities, final String filename) throws IOException {
		URLConnection connection = new URL(this.experimentsBase + "createStub").openConnection();
		connection.setRequestProperty("Content-Type", "application/json");
		connection.setDoOutput(true);
		try (PrintWriter pw = new PrintWriter(connection.getOutputStream())) {
			String parameterString = parameterListToJSONList(parameters);

			String requiredCapabilitiesString = "[]";
			if (!(requiredCapabilities.isEmpty())) {
				requiredCapabilitiesString = String.join("\"" + ", " + "\"", requiredCapabilities);
				// At leading and trailing \"
				requiredCapabilitiesString = "[" + "\"" + requiredCapabilitiesString + "\"" + "]";
			}

			/* we have to escape " */
			String scriptEsc = script.replace("\"", "\\\"");

			pw.print("{\"Script\":\"" + scriptEsc + "\",\"ScriptInstall\":\"\", \"RequiredCapabilities\":"
					+ requiredCapabilitiesString + ", \"Language\":\"Python\", \"PermutationFilter\":\"\",");
			pw.println("\"Repetitions\":0,\"Seeds\":0,\"RunName\":\"" + name + "\",\"FileName\":\"" + filename
					+ "\", \"TestRun\":\"False\", \"Parameters\":" + parameterString + "}");
			pw.flush();

			/* handle result */
			BufferedReader br = new BufferedReader(new InputStreamReader(connection.getInputStream()));
			StringBuffer sb = new StringBuffer();
			String tmp = null;
			while ((tmp = br.readLine()) != null) {
				sb.append(tmp);
			}
			final JsonElement root = new JsonParser().parse(sb.toString());
			return root.getAsJsonObject().get("ExperimentId").toString();
		}
	}

	/**
	 * Converts a list of parameters to a JSON style list
	 * 
	 * @param parameters
	 *            the list of parameters to convert
	 * @return the JSON style List
	 */
	private static String parameterListToJSONList(final List<Parameter> parameters) {
		StringBuilder parameterString = new StringBuilder("[");
		boolean firstOne = true;
		for (Parameter p : parameters) {
			if (!firstOne)
				parameterString.append(", ");
			firstOne = false;

			parameterString.append(p.toApiJson());
		}
		parameterString.append("]");
		return parameterString.toString();
	}

	public ExperimentInstanceResult getExperimentInstanceResult(final String experimentId,
			final String experimentInstanceId) {
		final String resultString = get(this.experimentsBase + experimentId + "/instances/" + experimentInstanceId + "/records");
		final JsonElement root = new JsonParser().parse(resultString);

		final List<Record> records = new ArrayList<>();
		for (JsonElement entry : root.getAsJsonArray()) {
			records.add(new Record(entry.getAsJsonObject().get("Key").toString(),
					entry.getAsJsonObject().get("Value").toString()));
		}

		return new ExperimentInstanceResult(experimentInstanceId, records);
	}

	public Status getExperimentInstanceStatus(final String experimentId, final String experimentInstanceId)
			throws IOException {
		final JsonElement root = getLog(experimentId, experimentInstanceId);
		JsonElement statusElement = root.getAsJsonObject().get("Status");

		try {
			int statusCode = Integer.parseInt(statusElement.getAsString());
			if (statusCode < Status.values().length) {
				return Status.values()[statusCode];
			} else {
				return Status.ERROR;
			}
		} catch (NumberFormatException e) {
			return Status.ERROR;
		}

	}

	public List<Record> getExperimentInstanceLogMessages(final String experimentId, final String experimentInstanceId)
			throws IOException {
		final JsonElement root = getLog(experimentId, experimentInstanceId);

		/*
		 * This is a bit on a hurry... build full representation of backend data
		 * structures...
		 */
		// root.getAsJsonObject().get("Log")

		final List<Record> logMessages = new ArrayList<>();
		for (JsonElement entry : root.getAsJsonObject().get("LogMessages").getAsJsonArray()) {
			logMessages.add(new Record(entry.getAsJsonObject().get("Key").toString(),
					entry.getAsJsonObject().get("Message").toString()));
		}

		return Collections.unmodifiableList(logMessages);
	}

	private JsonElement getLog(final String experimentId, final String experimentInstanceId) throws IOException {
		final String resultString = get(this.experimentsBase + experimentId + "/instances/" + experimentInstanceId);
		return new JsonParser().parse(resultString);
	}

	private String get(final String url) {
		try {
			URLConnection connection = new URL(url).openConnection();
			try (BufferedReader br = new BufferedReader(new InputStreamReader(connection.getInputStream()))) {
				StringBuffer sb = new StringBuffer();
				String tmp = null;
				while ((tmp = br.readLine()) != null) {
					sb.append(tmp);
				}
				return sb.toString();
			}
	    } catch (FileNotFoundException e) {
			throw new MaciException("Experiment or experimentInstance not found.");
		} catch (IOException e) {
			throw new MaciException("MACI server not found or communication error.");
		}
	}
}