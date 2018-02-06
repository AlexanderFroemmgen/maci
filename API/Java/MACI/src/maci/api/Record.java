package maci.api;

public class Record {
	
	private final String metricName;
	
	private final String metricValue;

	public Record(String metricName, String metricValue) {
		super();
		this.metricName = metricName;
		this.metricValue = metricValue;
	}
	
	@Override
	public String toString() {
		return "Record [metricName=" + metricName + ", metricValue=" + metricValue + "]";
	}

	public String getMetricName() {
		return metricName;
	}

	public String getMetricValue() {
		return metricValue;
	}
}
