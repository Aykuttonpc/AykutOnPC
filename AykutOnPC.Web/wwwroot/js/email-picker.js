/*
 * Email Picker — intercepts clicks on elements with [data-email-picker]
 * and opens a modal offering: Gmail · Outlook · Yahoo · Copy · Default mail app.
 *
 * Why: raw mailto: links launch whatever app is registered for the mailto
 * protocol on the OS. On Windows that is often "the default browser"
 * (Firefox/Edge/Chrome) which just shows a useless webmail-picker page, or
 * nothing at all. A modal chooser sidesteps this entirely.
 *
 * Usage in markup:
 *   <a href="mailto:foo@bar.com?subject=Hi&body=..."
 *      data-email-picker
 *      data-email="foo@bar.com"
 *      data-subject="Hi"
 *      data-body="Hello..."> ... </a>
 *
 * The href is preserved so the link still works if JS is disabled/blocked.
 */
(function () {
    'use strict';

    const PROVIDERS = [
        {
            id: 'gmail',
            label: 'Gmail',
            icon: 'fa-brands fa-google',
            url: (to, su, body) =>
                `https://mail.google.com/mail/?view=cm&fs=1&to=${encodeURIComponent(to)}` +
                `&su=${encodeURIComponent(su)}&body=${encodeURIComponent(body)}`
        },
        {
            id: 'outlook',
            label: 'Outlook',
            icon: 'fa-brands fa-microsoft',
            url: (to, su, body) =>
                `https://outlook.live.com/owa/?path=/mail/action/compose` +
                `&to=${encodeURIComponent(to)}&subject=${encodeURIComponent(su)}` +
                `&body=${encodeURIComponent(body)}`
        },
        {
            id: 'yahoo',
            label: 'Yahoo',
            icon: 'fa-brands fa-yahoo',
            url: (to, su, body) =>
                `https://compose.mail.yahoo.com/?to=${encodeURIComponent(to)}` +
                `&subject=${encodeURIComponent(su)}&body=${encodeURIComponent(body)}`
        }
    ];

    let modalEl = null;
    let lastFocused = null;

    function buildModal() {
        if (modalEl) return modalEl;

        modalEl = document.createElement('div');
        modalEl.className = 'email-picker-overlay';
        modalEl.setAttribute('role', 'dialog');
        modalEl.setAttribute('aria-modal', 'true');
        modalEl.setAttribute('aria-labelledby', 'email-picker-title');
        modalEl.hidden = true;

        modalEl.innerHTML = `
            <div class="email-picker-card" role="document">
                <button type="button" class="email-picker-close" aria-label="Close">&times;</button>
                <div class="email-picker-head">
                    <i class="fa-solid fa-envelope email-picker-icon"></i>
                    <h3 id="email-picker-title">Send an email</h3>
                    <p class="email-picker-addr"></p>
                </div>
                <div class="email-picker-options">
                    ${PROVIDERS.map(p => `
                        <button type="button" class="email-picker-btn" data-provider="${p.id}">
                            <i class="${p.icon}"></i><span>${p.label}</span>
                        </button>
                    `).join('')}
                    <button type="button" class="email-picker-btn" data-provider="copy">
                        <i class="fa-solid fa-copy"></i><span>Copy address</span>
                    </button>
                    <button type="button" class="email-picker-btn" data-provider="default">
                        <i class="fa-solid fa-envelope-open-text"></i><span>Default mail app</span>
                    </button>
                </div>
                <div class="email-picker-toast" role="status" aria-live="polite"></div>
            </div>
        `;

        document.body.appendChild(modalEl);

        // Close handlers
        modalEl.addEventListener('click', (e) => {
            if (e.target === modalEl) closeModal();
        });
        modalEl.querySelector('.email-picker-close').addEventListener('click', closeModal);
        document.addEventListener('keydown', (e) => {
            if (!modalEl.hidden && e.key === 'Escape') closeModal();
        });

        return modalEl;
    }

    function openModal(email, subject, body) {
        const m = buildModal();
        m.querySelector('.email-picker-addr').textContent = email;
        m.querySelector('.email-picker-toast').textContent = '';
        m.dataset.email = email;
        m.dataset.subject = subject || '';
        m.dataset.body = body || '';
        lastFocused = document.activeElement;
        m.hidden = false;
        requestAnimationFrame(() => m.classList.add('is-open'));
        const firstBtn = m.querySelector('.email-picker-btn');
        if (firstBtn) firstBtn.focus();

        // One-shot click handler for provider buttons
        m.querySelectorAll('.email-picker-btn').forEach(btn => {
            btn.onclick = () => handleChoice(btn.dataset.provider, m);
        });
    }

    function closeModal() {
        if (!modalEl) return;
        modalEl.classList.remove('is-open');
        setTimeout(() => {
            modalEl.hidden = true;
            if (lastFocused && typeof lastFocused.focus === 'function') {
                lastFocused.focus();
            }
        }, 180);
    }

    function showToast(msg) {
        const t = modalEl.querySelector('.email-picker-toast');
        t.textContent = msg;
        t.classList.add('is-visible');
        setTimeout(() => t.classList.remove('is-visible'), 1800);
    }

    function handleChoice(providerId, m) {
        const email   = m.dataset.email;
        const subject = m.dataset.subject;
        const body    = m.dataset.body;

        if (providerId === 'copy') {
            copyToClipboard(email)
                .then(() => showToast('Copied — ' + email))
                .catch(() => showToast('Copy failed — select manually'));
            return;
        }

        if (providerId === 'default') {
            const q = [];
            if (subject) q.push('subject=' + encodeURIComponent(subject));
            if (body)    q.push('body='    + encodeURIComponent(body));
            const href = 'mailto:' + email + (q.length ? '?' + q.join('&') : '');
            // mailto: needs top-level nav — don't open new tab.
            window.location.href = href;
            closeModal();
            return;
        }

        const provider = PROVIDERS.find(p => p.id === providerId);
        if (provider) {
            window.open(provider.url(email, subject, body), '_blank', 'noopener,noreferrer');
            closeModal();
        }
    }

    function copyToClipboard(text) {
        if (navigator.clipboard && window.isSecureContext) {
            return navigator.clipboard.writeText(text);
        }
        // Fallback for http/old browsers
        return new Promise((resolve, reject) => {
            try {
                const ta = document.createElement('textarea');
                ta.value = text;
                ta.setAttribute('readonly', '');
                ta.style.position = 'absolute';
                ta.style.left = '-9999px';
                document.body.appendChild(ta);
                ta.select();
                document.execCommand('copy');
                document.body.removeChild(ta);
                resolve();
            } catch (err) { reject(err); }
        });
    }

    // Delegate clicks from any element carrying [data-email-picker]
    document.addEventListener('click', (e) => {
        const trigger = e.target.closest('[data-email-picker]');
        if (!trigger) return;

        // Allow ctrl/middle-click to fall through to the mailto href (power users)
        if (e.ctrlKey || e.metaKey || e.shiftKey || e.button === 1) return;

        const email = trigger.dataset.email || extractEmail(trigger.getAttribute('href'));
        if (!email) return; // nothing to do

        e.preventDefault();
        openModal(email, trigger.dataset.subject || '', trigger.dataset.body || '');
    });

    function extractEmail(href) {
        if (!href || !href.toLowerCase().startsWith('mailto:')) return null;
        const body = href.slice(7); // strip "mailto:"
        const qIdx = body.indexOf('?');
        return decodeURIComponent(qIdx === -1 ? body : body.slice(0, qIdx));
    }
})();
