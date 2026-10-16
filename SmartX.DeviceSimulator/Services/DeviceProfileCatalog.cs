
using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public static class DeviceProfileCatalog
{
	public static List<DeviceProfile> CreateDefaults()
	{
		return new List<DeviceProfile>
		{
			new()
			{
				Name = "ESP32 Greenhouse Sensor",
				MacAddress = "02:53:58:00:00:01",
				Location = "Greenhouse A",
				Category = "Environmental",
				Kind = DeviceKind.EnvironmentalEsp32,
				Description =
					"Measures greenhouse temperature and soil moisture.",
				RandomSeed = 101
			},

			new()
			{
				Name = "Smart Power Plug",
				MacAddress = "02:53:58:00:00:02",
				Location = "Energy Monitoring Room",
				Category = "SmartPlug",
				Kind = DeviceKind.SmartPlug,
				Description =
					"Measures power consumption of connected equipment.",
				RandomSeed = 202
			},

			new()
			{
				Name = "ESP32 Irrigation Valve",
				MacAddress = "02:53:58:00:00:03",
				Location = "Hydroponic Zone B",
				Category = "IrrigationValve",
				Kind = DeviceKind.IrrigationValve,
				Description =
					"Reports irrigation valve position and moisture.",
				RandomSeed = 303
			},

			new()
			{
				Name = "Water Pump Controller",
				MacAddress = "02:53:58:00:00:04",
				Location = "Pump Station 1",
				Category = "PumpController",
				Kind = DeviceKind.PumpController,
				Description =
					"Measures pump power consumption during operation.",
				RandomSeed = 404
			}
		};
	}
}
