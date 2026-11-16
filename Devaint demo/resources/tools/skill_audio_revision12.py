"""Lock approved explosion families and raise large/planted bomb throw level."""
import copy
import json
import re
import render_basic_attack_audio as m


def revise(p, source):
    assert p['revision'] == 11
    actions = {a['id']: a for a in p['actions']}
    before = copy.deepcopy(actions)
    decisions = m.load(source / 'focused-selections.json')
    bomb = decisions['bombExplosion']
    raw = (source.parent / bomb['file']).read_bytes()
    assert m.sha(raw) == bomb['sha256']
    key = 'approved/bomb-B3'
    filename = 'approved-bomb-B3.wav'
    (source / 'audio' / filename).write_bytes(raw)
    samples = m.decode(raw)
    p['audio'][key] = dict(m.builder.audio_payload(raw, filename), gain=1,
        peak=max(map(abs, samples)), peakTime=m.sliding_peak(samples), peakEnvelopeStep=.01,
        peakEnvelope=[max(map(abs, samples[i:i+480])) for i in range(0, len(samples), 480)],
        source=dict(file=bomb['file'], sha256=bomb['sha256']), edit=dict(mode='approved-B3-byte-copy'))
    large_layer = next(l for l in actions['bomb_range_special']['candidates'][7]['layers'] if l['id'] == 'explosion')
    large = dict(audio=large_layer['audio'], gain=large_layer['gain'], name='확정 · 대형 폭탄 8번 폭발')
    assert p['sharedGroups']['large_explosion']['variants'][7]['audio'] == large['audio']
    large_audio = p['audio'][large['audio']]
    assert m.sha((source / 'audio' / large_audio['file']).read_bytes()) == large_audio['sha256']
    normal = dict(audio=key, gain=.8, name='확정 · 폭탄 B3')
    for family, variant in [('large_explosion', large), ('normal_explosion', normal), ('triple_explosion', normal)]:
        group = p['sharedGroups'][family]
        group.update(revision=12, fixed=True, variants=[copy.deepcopy(variant)], label=variant['name'])
    p['sharedGroups']['normal_explosion']['scope'] = '폭탄 기본공격 · 폭발 수리검'
    p['sharedGroups']['triple_explosion']['scope'] = '3연속 폭탄 · B3 원음 그대로'
    changed = []
    for a in p['actions']:
        if not any(l.get('group') in ('large_explosion', 'normal_explosion', 'triple_explosion') for r in a['candidates'] for l in r['layers']):
            continue
        changed.append(a['id'])
        a['candidateRevision'] = 12
        a['preserveLayerEditsFromRevision'] = 11
        a['confirmedComponents'] = {'explosion': copy.deepcopy(large if a['id'] in ('bomb_range_special', 'bomb_move_special', 'bomb_followup_special') else normal)}
        a['reviewReason'] = a['note'] = a['confirmedComponents']['explosion']['name'] + ' 공통 고정 · 게임 적용 전'
        for r in a['candidates']:
            for l in r['layers']:
                if l.get('group') in ('large_explosion', 'normal_explosion', 'triple_explosion'):
                    l.update(p['sharedGroups'][l['group']]['variants'][0])
                elif a['id'] in ('bomb_range_special', 'bomb_followup_special') and l['id'] == 'throw':
                    l['gain'] *= 2
            r['id'] = a['id'] + f'-v12-{r["number"]:02}'
            r['name'] = f'{r["number"]:02} · ' + ' → '.join(l['name'] for l in r['layers'])
            r['reason'] = a['reviewReason']
    # Current-sound comparison remains tied to Unity; the basic audition shows
    # the newly approved explosion explicitly as an unapplied preview override.
    p['basicExplosionPreview'] = dict(normal, status='user-confirmed; not applied')
    p['revision12UnchangedActions'] = {aid:a for aid,a in before.items() if aid not in changed}
    p['revision12PreviousActions'] = {aid:before[aid] for aid in changed}
    p.update(revision=12, reviewedActions=changed)
    decisions['explosionAssignments'] = dict(status='user-confirmed; not applied',
        large=dict(actions=['bomb_range_special','bomb_move_special','bomb_followup_special'],
            sourceAction='bomb_range_special', candidate=8, sourceRevision=11,
            audio=large['audio'], file='source/audio/'+large_audio['file'], sha256=large_audio['sha256'], gain=large['gain']),
        normal=dict(actions=['bomb_basic_review','bomb_basic_special','shuriken_range_special'],
            candidate='B3', file=bomb['file'], sha256=bomb['sha256'], gain=.8),
        throwGainAdjustment=dict(actions=['bomb_range_special','bomb_followup_special'], multiplier=2, decibels=6.0206))
    m.dump(source / 'focused-selections.json', decisions)
    return p


if __name__ == '__main__':
    asset = m.RES / 'audio/gamemaster-skill-audition'
    source = asset / 'source'
    page = asset / 'preview/index.html'
    p = json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>', page.read_text(encoding='utf8'))[1])
    revise(p, source)
    page.write_text((source/'template.html').read_text(encoding='utf8').replace('__DATA__',json.dumps(p,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',(source/'app.js').read_text(encoding='utf8')),encoding='utf8')
    m.dump(source/'recipes.json', dict(p, frames={k:{n:v[n] for n in ['source','sha256']} for k,v in p['frames'].items()},audio={k:{n:v for n,v in a.items() if n not in ['src','waveform']} for k,a in p['audio'].items()}))
    m.register_asset(asset, 'audio.gamemaster-skill-audition', 'revision 12: approved B3 and large candidate 8 explosions locked; large/planted throws +6dB; stars preserved; not applied')
    print('Revision 12: explosion assignments fixed; throw gain doubled; current Unity comparisons unchanged.')
