namespace SmartParkingLot.Cli;

public static class CliConstants
{
    public const string DEFAULT_PORT_NAME = "COM6";
    public const int DEFAULT_BAUD_RATE = 9600;
    public const int MONITOR_POLL_DELAY_MS = 1000;

    public const string DEFAULT_LOT_ID = "LOT-01";
    public const string DB_FILE_NAME = "smartparkingdb.db";
    public const string DB_FOLDER_NAME = "data";
    public const string ENTRY_GATE_ID = "G-01";
    public const string EXIT_GATE_ID = "G-02";
    public const int ENTRY_GATE_PIN = 10;
    public const int EXIT_GATE_PIN = 11;
}
