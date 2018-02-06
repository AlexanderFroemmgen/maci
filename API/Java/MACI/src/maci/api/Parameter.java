package maci.api;

public class Parameter {

	private final String parameterName;
	
	private final ParameterType parameterType;
	
	public Parameter(String parameterName, ParameterType parameterType) {
		super();
		this.parameterName = parameterName;
		this.parameterType = parameterType;
	}

	public String toApiJson() {
		return "{\"Name\":\"" + parameterName + "\",\"Type\":\"" + parameterType + "\", \"Purpose\":0,\"Unit\":\"\"}"; 
	}

	public enum ParameterType {
		String,
		Int,
		Float
	}
}
