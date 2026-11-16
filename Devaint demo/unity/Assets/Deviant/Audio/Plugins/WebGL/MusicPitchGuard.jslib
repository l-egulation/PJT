// Web builds only. Unity's Web audio channel keeps the last playback rate it was given and re-applies it to every clip
// it starts next, so a music voice could come in sped up (higher pitch) and stay that way. Background music never uses
// a pitch other than 1: any channel playing a clip at least `minSeconds` long is put back to rate 1.
var DeviantMusicPitchGuardLibrary = {
	// Returns how many channels were corrected this call (0 when nothing was off).
	DeviantMusicPitchGuard__deps: ['$WEBAudio'],
	DeviantMusicPitchGuard: function (minSeconds) {
		if (!WEBAudio.audioWebEnabled) return 0;
		var corrected = 0;
		for (var id in WEBAudio.audioInstances) {
			var channel = WEBAudio.audioInstances[id];
			if (!channel || typeof channel.playSoundClip !== 'function' || typeof channel.setPitch !== 'function') continue;
			var source = channel.source;
			if (!source) continue;
			var seconds = 0;
			if (source.buffer && source.buffer.duration) seconds = source.buffer.duration;
			else if (source.mediaElement && isFinite(source.mediaElement.duration)) seconds = source.mediaElement.duration;
			if (seconds < minSeconds) continue;
			var rate = source.isPausedMockNode ? source.playbackRate : source.playbackRate && source.playbackRate.value;
			if (channel.pitch === 1 && rate === 1) continue;
			try { channel.setPitch(1); corrected++; }
			catch (e) { console.error('DeviantMusicPitchGuard: ' + e); }
		}
		return corrected;
	},

	// Console diagnostics: every channel's stored pitch, its source's playback rate and the clip length and rate.
	DeviantMusicPitchDiagnostics__deps: ['$WEBAudio'],
	DeviantMusicPitchDiagnostics: function () {
		if (typeof window === 'undefined' || window.deviantAudioDump) return;
		window.deviantAudioDump = function () {
			var rows = [];
			for (var id in WEBAudio.audioInstances) {
				var channel = WEBAudio.audioInstances[id];
				if (!channel || typeof channel.playSoundClip !== 'function') continue;
				var source = channel.source;
				var buffer = source && source.buffer;
				rows.push({
					channel: id,
					storedPitch: channel.pitch,
					playbackRate: source ? (source.isPausedMockNode ? source.playbackRate : source.playbackRate && source.playbackRate.value) : null,
					seconds: buffer ? buffer.duration : source && source.mediaElement ? source.mediaElement.duration : null,
					clipSampleRate: buffer ? buffer.sampleRate : null
				});
			}
			console.log('AudioContext sampleRate: ' + (WEBAudio.audioContext ? WEBAudio.audioContext.sampleRate : 'none'));
			console.table(rows);
			return rows;
		};
	}
};

mergeInto(LibraryManager.library, DeviantMusicPitchGuardLibrary);
