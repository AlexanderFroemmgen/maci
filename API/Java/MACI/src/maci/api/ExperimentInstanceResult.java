package maci.api;

import java.util.List;

public class ExperimentInstanceResult {

	private final String experimentInstanceId;
	
	private final List<Record> records;

	public ExperimentInstanceResult(String experimentInstanceId, List<Record> records) {
		this.experimentInstanceId = experimentInstanceId;
		this.records = records;
	}

	@Override
	public String toString() {
		return "ExperimentInstanceResult [experimentInstanceId=" + experimentInstanceId + ", records=" + records + "]";
	}

	public String getSimulationInstanceId() {
		return experimentInstanceId;
	}

	public List<Record> getRecords() {
		return records;
	}
}
