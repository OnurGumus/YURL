const shortenerForm = document.getElementById('shortenerForm');
const urlInput = document.getElementById('urlInput');
const resultArea = document.getElementById('resultArea');
const shortenedUrlDisplay = document.getElementById('shortenedUrlDisplay');
const copyButton = document.getElementById('copyButton');
const copyFeedback = document.getElementById('copyFeedback');
const themeToggleButton = document.getElementById('themeToggle');
const bodyElement = document.body;
let currentTypingTimeout = null;

function applyTheme(theme) {
    if (theme === 'light') { bodyElement.classList.add('light-mode'); }
    else { bodyElement.classList.remove('light-mode'); }
}

const savedTheme = localStorage.getItem('theme');
if (savedTheme) { applyTheme(savedTheme); }
else {
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches) { applyTheme('light'); }
    else { applyTheme('dark'); }
}

themeToggleButton.addEventListener('click', () => {
    const currentTheme = bodyElement.classList.contains('light-mode') ? 'light' : 'dark';
    const newTheme = currentTheme === 'light' ? 'dark' : 'light';
    applyTheme(newTheme);
    localStorage.setItem('theme', newTheme);
});

shortenerForm.addEventListener('submit', function (event) {
    event.preventDefault();
    const longUrl = urlInput.value;
    if (!longUrl) { alert('AI_ERR//:INPUT_STREAM_REQUIRED'); return; } // Updated alert

    if (currentTypingTimeout) {
        clearTimeout(currentTypingTimeout);
        currentTypingTimeout = null;
    }

    const mockSlug = Array(6).fill(0).map(() => Math.random().toString(36).charAt(2)).join('');
    // UPDATED SHORT URL DOMAIN
    const fullShortenedUrl = `yurl.ai/${mockSlug}`;

    shortenedUrlDisplay.textContent = '';
    shortenedUrlDisplay.classList.remove('typing');
    resultArea.classList.add('visible');
    copyButton.disabled = true;
    copyFeedback.style.opacity = '0';
    copyFeedback.textContent = '';

    let i = 0;
    const speed = 70;

    function typeWriter() {
        if (i < fullShortenedUrl.length) {
            shortenedUrlDisplay.textContent += fullShortenedUrl.charAt(i);
            i++;
            if (i === 1 && !shortenedUrlDisplay.classList.contains('typing')) {
                shortenedUrlDisplay.classList.add('typing');
            }
            currentTypingTimeout = setTimeout(typeWriter, speed);
        } else {
            shortenedUrlDisplay.classList.remove('typing');
            copyButton.disabled = false;
            currentTypingTimeout = null;
        }
    }
    currentTypingTimeout = setTimeout(typeWriter, 50);
});

copyButton.addEventListener('click', function () {
    navigator.clipboard.writeText(shortenedUrlDisplay.textContent)
        .then(() => {
            // UPDATED COPY FEEDBACK
            copyFeedback.textContent = 'yurl_vector_copied_to_buffer';
            copyFeedback.style.opacity = '1';
            setTimeout(() => { copyFeedback.style.opacity = '0'; copyFeedback.textContent = ''; }, 2500);
        })
        .catch(err => {
            console.error('Clipboard write failed: ', err);
            // UPDATED COPY ERROR
            copyFeedback.textContent = 'ai_err//:buffer_access_denied';
            copyFeedback.style.opacity = '1';
            setTimeout(() => { copyFeedback.style.opacity = '0'; copyFeedback.textContent = ''; }, 3000);
        });
});