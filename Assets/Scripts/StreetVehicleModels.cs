using UnityEngine;

namespace NightChase
{
    public sealed class StreetVehicleModels : ScriptableObject
    {
        [SerializeField] GameObject sedan, hatchback, taxi, eastSport, eastJeep, westSedan;

        public GameObject ForVehicle(string vehicle)
        {
            switch (vehicle)
            {
                case "sedan": return sedan;
                case "hatchback": return hatchback;
                case "taxi": return taxi;
                case "east_sport": return eastSport;
                case "east_jeep": return eastJeep;
                case "west_sedan": return westSedan;
                default: return null;
            }
        }
    }
}
