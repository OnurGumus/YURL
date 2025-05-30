# OEmbed Support Examples

This implementation now supports multiple popular sites through oEmbed. Here's how different URLs will be processed:

## Supported Sites and Their Slug Prefixes

### Video Platforms
- **YouTube** (`ytb_`): `https://www.youtube.com/watch?v=dQw4w9WgXcQ` → `ytb_rick_astley` (3-word trimming)
- **Vimeo** (`vim_`): `https://vimeo.com/76979871` → `vim_big_buck`
- **TikTok** (`ttk_`): `https://www.tiktok.com/@user/video/7234567890` → `ttk_dance_trend` (3-word trimming)
- **Twitch** (`twh_`): `https://twitch.tv/videos/1234567` → `twh_gaming_stream`
- **DailyMotion** (`dm_`): `https://dailymotion.com/video/x8n9xyz` → `dm_tech_review`

### Audio Platforms
- **Spotify** (`spt_`): `https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT` → `spt_never_gonna`
- **SoundCloud** (`snd_`): `https://soundcloud.com/artist/track-name` → `snd_electronic_mix`
- **Mixcloud** (`mix_`): `https://mixcloud.com/dj/set-name` → `mix_house_session`

### Social Media
- **Instagram** (`ig_`): `https://www.instagram.com/p/CqGGJvwMK_i/` → `ig_sunset_photo` (3-word trimming)
- **Bluesky** (`bsk_`): `https://bsky.app/profile/user.bsky.social/post/3k7xyz` → `bsk_tech_update`
- **Reddit** (`rdt_`): `https://reddit.com/r/programming/comments/abc123` → `rdt_code_tips` (3-word trimming)
- **Twitter/X** (`twt_`): `https://twitter.com/user/status/123456` → `twt_breaking_news` (3-word trimming)

### News Sites
- **The Guardian** (`gdn_`): `https://theguardian.com/world/2024/article` → `gdn_climate_change` (3-word trimming)
- **New York Times** (`nyt_`): `https://nytimes.com/2024/01/tech-article` → `nyt_ai_breakthrough` (3-word trimming)

### Other Platforms
- **TED** (`ted_`): `https://ted.com/talks/speaker_title` → `ted_innovation_talk`
- **Medium** (`med_`): `https://medium.com/@user/article-title` → `med_startup_advice` (3-word trimming)
- **Flickr** (`flk_`): `https://flickr.com/photos/user/12345` → `flk_nature_photography`
- **SlideShare** (`sld_`): `https://slideshare.net/presentation` → `sld_business_strategy`
- **CodePen** (`cpn_`): `https://codepen.io/user/pen/abc123` → `cpn_css_animation`

## Features

1. **Automatic Title Extraction**: Uses each platform's oEmbed API to get proper titles
2. **Site-Specific Prefixes**: Each platform gets its own prefix for easy identification
3. **Smart Word Trimming**: Selected platforms (YouTube, TikTok, Instagram, Reddit, Twitter, Medium, Guardian, NYT) trim 3-word slugs to 2
4. **Fallback Support**: If oEmbed fails, uses hash with appropriate prefix
5. **Extensible Design**: Easy to add new platforms by updating the providers list

## Non-oEmbed URLs

URLs that don't match any oEmbed provider will continue to use the existing HTML parsing logic to extract titles from meta tags. 