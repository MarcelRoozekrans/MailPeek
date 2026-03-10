const Dashboard = (() => {
    let pathPrefix = '';
    let currentPage = 0;
    const pageSize = 25;
    let connection = null;
    let searchTimeout = null;
    let currentTag = null;

    function init(prefix) {
        pathPrefix = prefix.replace(/\/+$/, '');
        setupSignalR();
        setupEventListeners();
        loadMessages();

        if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission();
        }
    }

    // ── SignalR ──────────────────────────────────────────
    function setupSignalR() {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR not loaded, real-time updates disabled.');
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl(pathPrefix + '/hub')
            .withAutomaticReconnect()
            .build();

        connection.on('NewMessage', function (summary) {
            loadMessages();
            if ('Notification' in window && Notification.permission === 'granted') {
                var n = new Notification(summary.subject || '(no subject)', {
                    body: 'From: ' + (summary.from || 'unknown'),
                    tag: summary.id
                });
                n.onclick = function () {
                    window.focus();
                    showMessage(summary.id);
                    n.close();
                };
            }
        });

        connection.on('MessageDeleted', function () {
            loadMessages();
        });

        connection.on('MessagesCleared', function () {
            loadMessages();
        });

        connection.start().catch(function (err) {
            console.error('SignalR connection error:', err);
        });
    }

    // ── Event Listeners ─────────────────────────────────
    function setupEventListeners() {
        var searchInput = document.getElementById('search');
        searchInput.addEventListener('input', function () {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(function () {
                currentPage = 0;
                loadMessages();
            }, 300);
        });

        document.getElementById('clearAll').addEventListener('click', function () {
            if (confirm('Delete all messages?')) {
                clearAll();
            }
        });

        document.getElementById('backToInbox').addEventListener('click', function () {
            showInbox();
        });

        document.querySelectorAll('.tab').forEach(function (tab) {
            tab.addEventListener('click', function () {
                switchTab(tab.getAttribute('data-tab'));
            });
        });
    }

    // ── Load Messages ───────────────────────────────────
    async function loadMessages() {
        var search = document.getElementById('search').value;
        try {
            var url = pathPrefix + '/api/messages?page=' + currentPage + '&size=' + pageSize + '&search=' + encodeURIComponent(search);
            if (currentTag) {
                url += '&tag=' + encodeURIComponent(currentTag);
            }
            var response = await fetch(url);
            if (!response.ok) throw new Error('Failed to load messages');
            var data = await response.json();
            renderInbox(data);
        } catch (err) {
            console.error('Error loading messages:', err);
        }
    }

    // ── Render Inbox ────────────────────────────────────
    function renderInbox(data) {
        var tbody = document.getElementById('messageBody');
        var emptyState = document.getElementById('emptyState');
        var table = document.getElementById('messageTable');
        var items = data.items || [];
        var totalCount = data.totalCount || 0;

        // Update message count badge
        var badge = document.getElementById('messageCount');
        var unreadCount = data.unreadCount || 0;
        if (totalCount > 0) {
            badge.textContent = unreadCount > 0
                ? unreadCount + ' / ' + totalCount
                : totalCount + (totalCount === 1 ? ' message' : ' messages');
            badge.classList.add('visible');
        } else {
            badge.classList.remove('visible');
        }

        tbody.innerHTML = '';

        if (items.length === 0) {
            table.classList.add('hidden');
            emptyState.classList.add('visible');
            document.getElementById('pagination').innerHTML = '';
            return;
        }

        table.classList.remove('hidden');
        emptyState.classList.remove('visible');

        items.forEach(function (msg) {
            var tr = document.createElement('tr');
            if (!msg.isRead) {
                tr.classList.add('unread');
            }
            tr.addEventListener('click', function () {
                showMessage(msg.id);
            });

            var tdFrom = document.createElement('td');
            tdFrom.setAttribute('data-label', 'From');
            tdFrom.textContent = msg.from || '(unknown)';

            var tdTo = document.createElement('td');
            tdTo.setAttribute('data-label', 'To');
            tdTo.textContent = (msg.to || []).join(', ');

            var tdSubject = document.createElement('td');
            tdSubject.setAttribute('data-label', 'Subject');
            tdSubject.textContent = msg.subject || '(no subject)';
            if (msg.tags && msg.tags.length > 0) {
                msg.tags.forEach(function (tag) {
                    var pill = document.createElement('span');
                    pill.className = 'tag-pill';
                    pill.textContent = tag;
                    pill.style.backgroundColor = tagColor(tag);
                    pill.addEventListener('click', function (e) {
                        e.stopPropagation();
                        document.getElementById('search').value = '';
                        filterByTag(tag);
                    });
                    tdSubject.appendChild(pill);
                });
            }

            var tdDate = document.createElement('td');
            tdDate.setAttribute('data-label', 'Date');
            tdDate.textContent = formatDate(msg.receivedAt);

            var tdActions = document.createElement('td');
            tdActions.className = 'col-actions';
            if (msg.hasAttachments) {
                var attachSpan = document.createElement('span');
                attachSpan.textContent = '\uD83D\uDCCE ';
                attachSpan.title = 'Has attachments';
                tdActions.appendChild(attachSpan);
            }
            var delBtn = document.createElement('button');
            delBtn.className = 'btn-delete-row';
            delBtn.title = 'Delete';
            delBtn.textContent = '\u2715';
            delBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                deleteMessage(msg.id);
            });
            tdActions.appendChild(delBtn);

            tr.appendChild(tdFrom);
            tr.appendChild(tdTo);
            tr.appendChild(tdSubject);
            tr.appendChild(tdDate);
            tr.appendChild(tdActions);

            tbody.appendChild(tr);
        });

        renderPagination(totalCount);
    }

    // ── Pagination ──────────────────────────────────────
    function renderPagination(totalCount) {
        var container = document.getElementById('pagination');
        container.innerHTML = '';

        var totalPages = Math.ceil(totalCount / pageSize);
        if (totalPages <= 1) return;

        var prev = document.createElement('button');
        prev.textContent = '\u2190 Prev';
        prev.disabled = currentPage === 0;
        prev.addEventListener('click', function () {
            if (currentPage > 0) {
                currentPage--;
                loadMessages();
            }
        });

        var info = document.createElement('span');
        info.className = 'page-info';
        info.textContent = 'Page ' + (currentPage + 1) + ' of ' + totalPages + ' (' + totalCount + ' messages)';

        var next = document.createElement('button');
        next.textContent = 'Next \u2192';
        next.disabled = currentPage >= totalPages - 1;
        next.addEventListener('click', function () {
            if (currentPage < totalPages - 1) {
                currentPage++;
                loadMessages();
            }
        });

        container.appendChild(prev);
        container.appendChild(info);
        container.appendChild(next);
    }

    // ── Show Message Detail ─────────────────────────────
    async function showMessage(id) {
        try {
            var response = await fetch(pathPrefix + '/api/messages/' + id);
            if (!response.ok) throw new Error('Failed to load message');
            var msg = await response.json();

            if (!msg.isRead) {
                fetch(pathPrefix + '/api/messages/' + id + '/read', { method: 'PUT' })
                    .catch(function(err) { console.error('Failed to mark as read:', err); });
            }

            document.getElementById('inbox').classList.add('hidden');
            document.getElementById('messageDetail').classList.remove('hidden');

            // Render header
            var header = document.getElementById('detailHeader');
            var subjectHtml = '<div class="detail-subject">' + escapeHtml(msg.subject || '(no subject)') + '</div>';
            var metaParts = [];
            metaParts.push('<span><strong>From:</strong> ' + escapeHtml(msg.from || '') + '</span>');
            metaParts.push('<span><strong>To:</strong> ' + escapeHtml((msg.to || []).join(', ')) + '</span>');
            if (msg.cc && msg.cc.length > 0) {
                metaParts.push('<span><strong>Cc:</strong> ' + escapeHtml(msg.cc.join(', ')) + '</span>');
            }
            metaParts.push('<span><strong>Date:</strong> ' + formatDate(msg.receivedAt) + '</span>');
            if (msg.tags && msg.tags.length > 0) {
                var tagHtml = msg.tags.map(function(t) {
                    return '<span class="tag-pill" style="background-color:' + tagColor(t) + '">' + escapeHtml(t) + '</span>';
                }).join(' ');
                metaParts.push('<span>' + tagHtml + '</span>');
            }
            header.innerHTML = subjectHtml + '<div class="detail-meta">' + metaParts.join('') + '</div>';

            // HTML preview via iframe
            var iframe = document.getElementById('htmlPreview');
            iframe.src = pathPrefix + '/api/messages/' + id + '/html';

            // Text body
            var textPre = document.getElementById('textPreview');
            if (msg.textBody) {
                textPre.textContent = msg.textBody;
                textPre.classList.remove('empty-placeholder');
            } else {
                textPre.textContent = 'No text body';
                textPre.classList.add('empty-placeholder');
            }

            // Headers table
            var headersTable = document.getElementById('headersTable');
            headersTable.innerHTML = '';
            var headers = msg.headers || {};
            Object.keys(headers).forEach(function (key) {
                var tr = document.createElement('tr');
                var tdKey = document.createElement('td');
                tdKey.textContent = key;
                var tdVal = document.createElement('td');
                tdVal.textContent = headers[key];
                tr.appendChild(tdKey);
                tr.appendChild(tdVal);
                headersTable.appendChild(tr);
            });

            // Attachments
            var attachList = document.getElementById('attachmentsList');
            attachList.innerHTML = '';
            var attachments = msg.attachments || [];
            if (attachments.length === 0) {
                var li = document.createElement('li');
                li.textContent = 'No attachments.';
                attachList.appendChild(li);
            } else {
                attachments.forEach(function (att, index) {
                    var li = document.createElement('li');
                    var a = document.createElement('a');
                    a.href = pathPrefix + '/api/messages/' + id + '/attachments/' + index;
                    a.textContent = att.fileName || 'attachment-' + index;
                    a.setAttribute('download', att.fileName || 'attachment');
                    var sizeSpan = document.createElement('span');
                    sizeSpan.className = 'attachment-size';
                    sizeSpan.textContent = att.contentType || '';
                    li.appendChild(a);
                    li.appendChild(sizeSpan);
                    attachList.appendChild(li);
                });
            }

            // Links
            loadLinks(id);
            if (connection) {
                connection.off('LinkCheckComplete');
                connection.on('LinkCheckComplete', function (completedId) {
                    if (completedId === id) loadLinks(id);
                });
            }

            // Default to HTML tab
            switchTab('html');
        } catch (err) {
            console.error('Error loading message:', err);
        }
    }

    // ── Show Inbox ──────────────────────────────────────
    function showInbox() {
        document.getElementById('inbox').classList.remove('hidden');
        document.getElementById('messageDetail').classList.add('hidden');
        loadMessages();
    }

    // ── Tab Switching ───────────────────────────────────
    function switchTab(tabName) {
        document.querySelectorAll('.tab').forEach(function (t) {
            t.classList.toggle('active', t.getAttribute('data-tab') === tabName);
        });
        document.querySelectorAll('.tab-panel').forEach(function (p) {
            p.classList.toggle('active', p.id === 'tab-' + tabName);
        });
    }

    // ── Delete Message ──────────────────────────────────
    async function deleteMessage(id) {
        try {
            await fetch(pathPrefix + '/api/messages/' + id, { method: 'DELETE' });
            showInbox();
            loadMessages();
        } catch (err) {
            console.error('Error deleting message:', err);
        }
    }

    // ── Clear All ───────────────────────────────────────
    async function clearAll() {
        try {
            await fetch(pathPrefix + '/api/messages', { method: 'DELETE' });
            showInbox();
            loadMessages();
        } catch (err) {
            console.error('Error clearing messages:', err);
        }
    }

    // ── Helpers ─────────────────────────────────────────
    function tagColor(tag) {
        var hash = 0;
        for (var i = 0; i < tag.length; i++) {
            hash = tag.charCodeAt(i) + ((hash << 5) - hash);
        }
        var hue = Math.abs(hash) % 360;
        return 'hsl(' + hue + ', 60%, 45%)';
    }

    function filterByTag(tag) {
        currentTag = tag === currentTag ? null : tag;
        currentPage = 0;
        loadMessages();
    }

    function formatDate(dateStr) {
        if (!dateStr) return '';
        try {
            return new Date(dateStr).toLocaleString();
        } catch (e) {
            return dateStr;
        }
    }

    function escapeHtml(str) {
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

    // ── Load Links ───────────────────────────────────────
    async function loadLinks(id) {
        var statusEl = document.getElementById('linksStatus');
        var tableEl = document.getElementById('linksTable');
        var tbody = document.getElementById('linksBody');
        try {
            var response = await fetch(pathPrefix + '/api/messages/' + id + '/links');
            if (response.status === 202) {
                statusEl.textContent = 'Checking links...';
                statusEl.classList.remove('hidden');
                tableEl.classList.add('hidden');
                return;
            }
            var links = await response.json();
            statusEl.classList.add('hidden');
            tbody.innerHTML = '';
            if (!links || links.length === 0) {
                statusEl.textContent = 'No links found.';
                statusEl.classList.remove('hidden');
                tableEl.classList.add('hidden');
                return;
            }
            tableEl.classList.remove('hidden');
            links.forEach(function (link) {
                var tr = document.createElement('tr');
                var tdUrl = document.createElement('td');
                var a = document.createElement('a');
                a.href = link.url;
                a.textContent = link.url;
                a.target = '_blank';
                a.rel = 'noopener';
                tdUrl.appendChild(a);
                var tdStatus = document.createElement('td');
                var statusText = link.statusCode ? link.status + ' (' + link.statusCode + ')' : link.status;
                var statusSpan = document.createElement('span');
                var statusClass = String(link.status).toLowerCase();
                statusSpan.className = 'link-status link-status-' + statusClass;
                statusSpan.textContent = statusText;
                tdStatus.appendChild(statusSpan);
                tr.appendChild(tdUrl);
                tr.appendChild(tdStatus);
                tbody.appendChild(tr);
            });
        } catch (err) {
            console.error('Error loading links:', err);
        }
    }

    return { init: init };
})();
