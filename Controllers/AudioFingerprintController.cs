using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SoundFingerprinting;
using SoundFingerprinting.AddictedCS.Demo.Repositories;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.Data;
using SoundFingerprinting.InMemory;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioFingerprintingService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AudioFingerprintController : ControllerBase
    {
        private readonly IAudioFingerprintRepository _repo;
        private readonly IModelService _modelService; // store fingerprints in RAM
        static readonly IAudioService audioService = new SoundFingerprintingAudioService(); // default audio library

        private readonly ILogger<AudioFingerprintController> _logger;

        public AudioFingerprintController(ILogger<AudioFingerprintController> logger, IAudioFingerprintRepository repo)
        {
            _logger = logger;
            _repo = repo;
            _modelService = new InMemoryModelService();
        }

        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new List<string>
           {
               "A","B","C"
           };
        }

        [HttpPost]
        [Route("generate-audio-fingerprints")]
        public  void GenerateAudioFingerprints(AudioDetails audioDetails)
        {
            var trackName = GetTrackName(audioDetails.AudioFileName);

            var result = FingerprintCommandBuilder.Instance
                                        .BuildFingerprintCommand()
                                        .From("./" + audioDetails.AudioFileName)
                                        .WithFingerprintConfig(new HighPrecisionFingerprintConfiguration())
                                        .UsingServices(audioService)
                                        .Hash();
            var hashedFingerprints = result.Result;
            if(hashedFingerprints != null && hashedFingerprints.Any())
            {
                Console.WriteLine("Total audio fingerprints generated of " + audioDetails.AudioFileName + " are " + hashedFingerprints.Count);
                _repo.SaveAudioFingerprints(hashedFingerprints, trackName);
            }
            else
            {
                Console.WriteLine("OOPS... Hash fingerprints have not generated.");
            }
        }

        [HttpGet]
        [Route("get-matching-audio")]
        public string GetBestMatchingAudioTrack([FromQuery] string audioFileName)
        {
            int secondsToAnalyze = 3; // number of seconds to analyze from query file
            int startAtSecond = 0; // start at the begining
            var queryAudioFilePath = "./" + audioFileName;

            var audioFingerprints = _repo.GetAudioFingerprintHashes();
            foreach (var audioFingerprint in audioFingerprints)
            {
                _modelService.Insert(new TrackInfo(audioFingerprint.Key, audioFingerprint.Key, audioFingerprint.Key), new Hashes(audioFingerprint.Value, 1));
            }

            var queryResult = QueryCommandBuilder.Instance.BuildQueryCommand()
                                                .From(queryAudioFilePath, secondsToAnalyze, startAtSecond)
                                                .WithQueryConfig(new HighPrecisionQueryConfiguration())
                                                .UsingServices(_modelService, audioService)
                                                .Query();
            var bestMatch = queryResult.Result.BestMatch;
            return bestMatch?.Track.Id;
        }

        private static string GetTrackName(string audioFileName)
        {
            string trackName;
            switch (audioFileName)
            {
                case "WelcomeABC_Alice.wav": trackName = "Welcome Audio by Alice"; break;
                case "WelcomeABC_George.wav": trackName = "Welcome Audio by George"; break;
                case "WelcomeABC_Jenna.wav": trackName = "Welcome Audio by Jenna"; break;
                case "AppointmentScheduling_AliceVoice.wav": trackName = "Appointment scheduling audio by Alice"; break;
                case "AppointmentTypes_AliceVoice.wav": trackName = "Appointment types audio by Alice"; break;
                default: throw new NotImplementedException();
            }
            return trackName;
        }
    }

    public class AudioDetails
    {
        public string AudioFileName { get; set; }
    }
}
