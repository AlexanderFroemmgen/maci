

import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.PrintWriter;

/**
 * 
 * @author Alexander Froemmmgen
 * 
 * MaciMetrics provides a lightweight integration of MACI for Java projects.
 * 
 * Metrics are stored in temporary JSON files and integrated into the result set by the Python framework.
 *
 */
public class MaciMetrics {

	/* Design decision: no external dependencies, no libraries, keep it simple */
	
	// TODO: A lot of performance improvements are possible... But we should start with some Benchmarking
	
	private static PrintWriter pwRecord;
	private static PrintWriter pwMessage;
	
	private static void writeRecord(final String s) throws IOException {
		if(null == pwRecord) {
			pwRecord = new PrintWriter(new File("result_tmp.json"));
		}
		pwRecord.println(s);
		pwRecord.flush();
	}
	
	public static void record(final String key, final int value) throws IOException {
		record(key, value, 0);
	}
	
	public static void record(final String key, final int value, final long time) throws IOException {
		writeRecord("{\"key\":\"" + key + "\", \"value\":" + value + ", \"offset\":" + time + "}");
	}
	
	public static void record(final String key, final String value) throws IOException {
		record(key, value, 0);
	}
	
	public static void record(final String key, final String value, final long time) throws IOException {
		writeRecord("{\"key\":\"" + key + "\", \"value\":\"" + value + "\", \"offset\":" + time + "}");
	}
	
	public static void record(final String key, final long value) throws IOException {
		record(key, value, 0);
	}
	
	public static void record(final String key, final long value, final long time) throws IOException {
		writeRecord("{\"key\":\"" + key + "\", \"value\":" + value + ", \"offset\":" + time + "}");
	}
	
	private static void writeMessage(final String s) throws FileNotFoundException {
		if(null == pwMessage) {
			pwMessage = new PrintWriter(new File("message_tmp.json"));
		}
		pwMessage.println(s);
		pwMessage.flush();
	}
	
	public static void log(final String key, final String message) throws FileNotFoundException {
		writeMessage("{\"key\":\"" + key + "\", \"message\":" + message + ", \"offset\": 0, \"type\":0}");
	}
	
	public static void warn(final String key, final String message) throws FileNotFoundException {
		writeMessage("{\"key\":\"" + key + "\", \"message\":" + message + ", \"offset\": 0, \"type\":1}");
	}
}
