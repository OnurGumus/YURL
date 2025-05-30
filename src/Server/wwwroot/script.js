const shortenerForm = document.getElementById('shortenerForm');
const urlInput = document.getElementById('urlInput');
const resultArea = document.getElementById('resultArea');
const shortenedUrlDisplay = document.getElementById('shortenedUrlDisplay');
const copyButton = document.getElementById('copyButton');
const copyFeedback = document.getElementById('copyFeedback');
const shrinkButton = document.querySelector('.shrink-button');
const buttonText = shrinkButton?.querySelector('.button-text');
const buttonLoader = shrinkButton?.querySelector('.button-loader');
const buttonCountdown = document.getElementById('buttonCountdown');
let currentTypingTimeout = null;
let countdownInterval = null;

// Only proceed with form functionality if the form exists
if (shortenerForm && urlInput && resultArea && shortenedUrlDisplay && copyButton && copyFeedback && shrinkButton) {

// Capture initial button width to maintain consistent size
let initialButtonWidth = null;

function captureButtonWidth() {
    if (copyButton && !initialButtonWidth) {
        // Wait for button to be visible and rendered
        if (copyButton.offsetWidth > 0) {
            initialButtonWidth = copyButton.offsetWidth;
            copyButton.style.width = initialButtonWidth + 'px';
        }
    }
}

function isValidUrlFormat(url) {
    if (!url) {
        alert('URL input cannot be empty. Please enter a URL.');
        return false;
    }
    if (url.includes(' ')) {
        alert('Invalid URL format. URLs cannot contain spaces.');
        return false;
    }
    // Basic check for a dot, not at the beginning or end, and something on both sides of the first dot.
    const firstDotIndex = url.indexOf('.');
    if (firstDotIndex === -1 || firstDotIndex === 0 || firstDotIndex === url.length - 1) {
        alert('Invalid URL format. Please ensure it includes a valid domain name (e.g., example.com).');
        return false;
    }
    return true;
}

shortenerForm.addEventListener('submit', async function (event) {
    event.preventDefault();
    const longUrl = urlInput.value.trim(); // Trim whitespace

    if (!isValidUrlFormat(longUrl)) {
        return; // Stop if validation fails
    }

    if (currentTypingTimeout) {
        clearTimeout(currentTypingTimeout);
        currentTypingTimeout = null;
    }

    // Disable button and show loader
    shrinkButton.disabled = true;
    shrinkButton.classList.add('loading');
    startCountdown();

    let slug = '';
    try {
        const requestBody = { url: longUrl };
        console.log('Sending request body:', requestBody);
        
        const response = await fetch('/api/slug', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(requestBody),
        });
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        slug = await response.json();
        console.log('Received slug:', slug);
    } catch (error) {
        console.error('Failed to fetch slug:', error);
        alert('Failed to generate a short link. Please try again.');
        // Enable button and hide loader even if there's an error
        shrinkButton.disabled = false;
        shrinkButton.classList.remove('loading');
        stopCountdown();
        return;
    }

    // Enable button and hide loader
    shrinkButton.disabled = false;
    shrinkButton.classList.remove('loading');
    stopCountdown();

    // UPDATED SHORT URL DOMAIN
    const fullShortenedUrl = `${window.location.host}/${slug}`;

    shortenedUrlDisplay.textContent = '';
    shortenedUrlDisplay.classList.remove('typing');
    resultArea.classList.add('visible');
    copyButton.disabled = true;
    copyFeedback.style.opacity = '0';
    copyFeedback.textContent = '';

    // Capture button width once it's visible
    setTimeout(() => captureButtonWidth(), 50);

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
    const hostname = window.location.host.toUpperCase();
    const slug = shortenedUrlDisplay.textContent.split('/').pop();
    const urlToCopy = `https://${hostname}/${slug}`;
    
    // Add visual feedback to button
    copyButton.classList.add('copied');
    const originalText = copyButton.textContent;
    copyButton.textContent = 'Copied!';
    
    navigator.clipboard.writeText(urlToCopy)
        .then(() => {
            // UPDATED COPY FEEDBACK
            copyFeedback.textContent = 'yurl_vector_copied_to_buffer';
            copyFeedback.style.opacity = '1';
            
            // Reset button after delay
            setTimeout(() => {
                copyButton.classList.remove('copied');
                copyButton.textContent = originalText;
                copyFeedback.style.opacity = '0';
                copyFeedback.textContent = '';
            }, 2500);
        })
        .catch(err => {
            console.error('Clipboard write failed: ', err);
            // UPDATED COPY ERROR
            copyFeedback.textContent = 'ai_err//:buffer_access_denied';
            copyFeedback.style.opacity = '1';
            
            // Reset button after delay
            setTimeout(() => {
                copyButton.classList.remove('copied');
                copyButton.textContent = originalText;
                copyFeedback.style.opacity = '0';
                copyFeedback.textContent = '';
            }, 3000);
        });
});

function startCountdown() {
    let count = 8;
    if (buttonCountdown) {
        buttonCountdown.textContent = count;
        
        countdownInterval = setInterval(() => {
            count--;
            buttonCountdown.textContent = count;
            
            if (count <= 0) {
                stopCountdown();
            }
        }, 1000);
    }
}

function stopCountdown() {
    if (countdownInterval) {
        clearInterval(countdownInterval);
        countdownInterval = null;
    }
    if (buttonCountdown) {
        buttonCountdown.textContent = '';
    }
}

} // End of form functionality check