package maci.api;

public class ParameterAssignment {

	private final String parameterName;
	
	private final String parameterValue;

	public ParameterAssignment(String parameterName, String parameterValue) {
		this.parameterName = parameterName;
		this.parameterValue = parameterValue;
	}
	
	public String toApiJson() {
		return "{\"ParameterName\":\"" + parameterName + "\",\"Value\":\"" + parameterValue + "\"}"; 
	}
}
