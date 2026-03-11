const Dashboard = (() => {
    let pathPrefix = '';
    let currentPage = 0;
    const pageSize = 25;
    let connection = null;
    let searchTimeout = null;
    let currentTag = null;
    let selectedIds = new Set();
    let currentSort = 'date';
    let sortDescending = true;
    let focusedIndex = -1;

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

        connection.on('MessagesDeleted', function () {
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

        // Select all checkbox
        document.getElementById('selectAll').addEventListener('change', function (e) {
            var checkboxes = document.querySelectorAll('.row-checkbox');
            checkboxes.forEach(function (cb) {
                cb.checked = e.target.checked;
                var tr = cb.closest('tr');
                var id = tr.getAttribute('data-id');
                if (e.target.checked) {
                    selectedIds.add(id);
                    tr.classList.add('selected');
                } else {
                    selectedIds.delete(id);
                    tr.classList.remove('selected');
                }
            });
            updateBulkBar();
        });

        // Bulk bar buttons
        document.getElementById('bulkDelete').addEventListener('click', bulkDelete);
        document.getElementById('bulkClear').addEventListener('click', clearSelection);

        document.addEventListener('keydown', handleKeyDown);
        document.getElementById('keyboardHelp').addEventListener('click', function (e) {
            if (e.target.id === 'keyboardHelp') hideKeyboardHelp();
        });

        // Sortable column headers
        document.querySelectorAll('#messageTable thead th.sortable').forEach(function (th) {
            th.addEventListener('click', function () {
                setSort(th.getAttribute('data-sort'));
            });
        });
    }

    // ── Load Messages ───────────────────────────────────
    async function loadMessages() {
        selectedIds.clear();
        var search = document.getElementById('search').value;
        try {
            var url = pathPrefix + '/api/messages?page=' + currentPage + '&size=' + pageSize + '&search=' + encodeURIComponent(search);
            if (currentTag) {
                url += '&tag=' + encodeURIComponent(currentTag);
            }
            if (currentSort) {
                url += '&sortBy=' + currentSort + '&sortDesc=' + sortDescending;
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
        focusedIndex = -1;

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
            tr.setAttribute('data-id', msg.id);
            if (!msg.isRead) {
                tr.classList.add('unread');
            }
            tr.addEventListener('click', function () {
                showMessage(msg.id);
            });

            var tdCheck = document.createElement('td');
            tdCheck.className = 'col-checkbox';
            var checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.className = 'row-checkbox';
            checkbox.checked = selectedIds.has(msg.id);
            checkbox.addEventListener('click', function (e) {
                e.stopPropagation();
            });
            checkbox.addEventListener('change', function (e) {
                if (e.target.checked) {
                    selectedIds.add(msg.id);
                    tr.classList.add('selected');
                } else {
                    selectedIds.delete(msg.id);
                    tr.classList.remove('selected');
                }
                updateBulkBar();
                updateSelectAll();
            });
            tdCheck.appendChild(checkbox);

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

            if (msg.snippet) {
                var snippetEl = document.createElement('div');
                snippetEl.className = 'msg-snippet';
                snippetEl.textContent = msg.snippet;
                tdSubject.appendChild(snippetEl);
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

            tr.appendChild(tdCheck);
            tr.appendChild(tdFrom);
            tr.appendChild(tdTo);
            tr.appendChild(tdSubject);
            tr.appendChild(tdDate);
            tr.appendChild(tdActions);

            tbody.appendChild(tr);
        });

        renderPagination(totalCount);
        updateSelectAll();
        updateBulkBar();
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

            // Compatibility
            loadCompatibility(id);
            if (connection) {
                connection.off('HtmlCompatibilityCheckComplete');
                connection.on('HtmlCompatibilityCheckComplete', function (completedId) {
                    if (completedId === id) loadCompatibility(id);
                });
            }

            // Spam
            loadSpam(id);
            if (connection) {
                connection.off('SpamCheckComplete');
                connection.on('SpamCheckComplete', function (completedId) {
                    if (completedId === id) loadSpam(id);
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

    // ── Load Compatibility ────────────────────────────────
    async function loadCompatibility(id) {
        var container = document.getElementById('compatibilityContent');
        container.innerHTML = '<p class="text-muted">Checking compatibility...</p>';

        try {
            var resp = await fetch(pathPrefix + '/api/messages/' + id + '/compatibility');
            if (resp.status === 202) {
                container.innerHTML = '<p class="text-muted">Compatibility check in progress...</p>';
                return;
            }
            var data = await resp.json();

            var scoreClass = data.score >= 80 ? 'score-good' : data.score >= 50 ? 'score-warn' : 'score-bad';
            var html = '<div class="compat-score ' + scoreClass + '">' + data.score + '/100</div>';

            if (data.issues && data.issues.length > 0) {
                html += '<div class="compat-issues">';
                data.issues.forEach(function (issue) {
                    html += '<div class="compat-issue severity-' + issue.severity.toLowerCase() + '">'
                        + '<div class="compat-issue-title">' + escapeHtml(issue.description) + '</div>'
                        + '<div class="compat-issue-clients">Affected: ' + escapeHtml(issue.affectedClients.join(', ')) + '</div>'
                        + '</div>';
                });
                html += '</div>';
            } else {
                html += '<p class="text-muted">No compatibility issues found.</p>';
            }

            container.innerHTML = html;
        } catch (err) {
            console.error('Error loading compatibility:', err);
        }
    }

    // ── Load Spam ─────────────────────────────────────────
    async function loadSpam(id) {
        var container = document.getElementById('spamContent');
        container.innerHTML = '<p class="text-muted">Checking spam score...</p>';

        try {
            var resp = await fetch(pathPrefix + '/api/messages/' + id + '/spam');
            if (resp.status === 202) {
                container.innerHTML = '<p class="text-muted">Spam analysis in progress...</p>';
                return;
            }
            var data = await resp.json();

            var riskClass = data.score <= 5 ? 'risk-low' : data.score <= 12 ? 'risk-medium' : 'risk-high';
            var riskLabel = data.score <= 5 ? 'Low Risk' : data.score <= 12 ? 'Medium Risk' : 'High Risk';
            var html = '<div class="spam-score ' + riskClass + '">' + data.score.toFixed(1) + ' \u2014 ' + riskLabel + '</div>';
            html += '<div class="spam-source">Source: ' + escapeHtml(data.source) + '</div>';

            if (data.rules && data.rules.length > 0) {
                data.rules.forEach(function (rule) {
                    html += '<div class="spam-rule">'
                        + '<div><span class="spam-rule-name">' + escapeHtml(rule.name) + '</span><br>'
                        + '<span class="spam-rule-desc">' + escapeHtml(rule.description) + '</span></div>'
                        + '<div class="spam-rule-score">+' + rule.score.toFixed(1) + '</div>'
                        + '</div>';
                });
            } else {
                html += '<p class="text-muted">No spam indicators found.</p>';
            }

            container.innerHTML = html;
        } catch (err) {
            console.error('Error loading spam:', err);
        }
    }

    // ── Bulk Operations ──────────────────────────────────
    function updateBulkBar() {
        var bar = document.getElementById('bulkBar');
        var count = document.getElementById('bulkCount');
        if (selectedIds.size > 0) {
            bar.classList.remove('hidden');
            bar.classList.add('visible');
            count.textContent = selectedIds.size + ' selected';
        } else {
            bar.classList.add('hidden');
            bar.classList.remove('visible');
        }
    }

    function updateSelectAll() {
        var selectAll = document.getElementById('selectAll');
        var checkboxes = document.querySelectorAll('.row-checkbox');
        if (checkboxes.length === 0) {
            selectAll.checked = false;
            selectAll.indeterminate = false;
            return;
        }
        var checkedCount = selectedIds.size;
        selectAll.checked = checkedCount === checkboxes.length && checkedCount > 0;
        selectAll.indeterminate = checkedCount > 0 && checkedCount < checkboxes.length;
    }

    async function bulkDelete() {
        if (selectedIds.size === 0) return;
        if (!confirm('Delete ' + selectedIds.size + ' message(s)?')) return;
        try {
            var response = await fetch(pathPrefix + '/api/messages/bulk', {
                method: 'DELETE',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ ids: Array.from(selectedIds) })
            });
            if (!response.ok) throw new Error('Bulk delete failed');
            selectedIds.clear();
            loadMessages();
        } catch (err) {
            console.error('Error during bulk delete:', err);
        }
    }

    function clearSelection() {
        selectedIds.clear();
        document.querySelectorAll('.row-checkbox').forEach(function (cb) { cb.checked = false; });
        document.querySelectorAll('#messageTable tbody tr.selected').forEach(function (tr) { tr.classList.remove('selected'); });
        updateBulkBar();
        updateSelectAll();
    }

    // ── Sorting ──────────────────────────────────────────
    function setSort(field) {
        if (currentSort === field) {
            sortDescending = !sortDescending;
        } else {
            currentSort = field;
            sortDescending = true;
        }
        currentPage = 0;
        updateSortHeaders();
        loadMessages();
    }

    function updateSortHeaders() {
        document.querySelectorAll('#messageTable thead th.sortable').forEach(function (th) {
            var field = th.getAttribute('data-sort');
            var arrow = th.querySelector('.sort-arrow');
            if (field === currentSort) {
                th.classList.add('active');
                if (!arrow) {
                    arrow = document.createElement('span');
                    arrow.className = 'sort-arrow';
                    th.appendChild(arrow);
                }
                arrow.innerHTML = sortDescending ? '&#9660;' : '&#9650;';
            } else {
                th.classList.remove('active');
                if (arrow) arrow.remove();
            }
        });
    }

    // ── Keyboard Navigation ──────────────────────────────
    function handleKeyDown(e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

        var rows = document.querySelectorAll('#messageTable tbody tr');
        var detailVisible = document.getElementById('messageDetail').style.display !== 'none';

        switch (e.key) {
            case 'j':
                e.preventDefault();
                if (!detailVisible && rows.length > 0) {
                    focusedIndex = Math.min(focusedIndex + 1, rows.length - 1);
                    updateFocusedRow(rows);
                }
                break;
            case 'k':
                e.preventDefault();
                if (!detailVisible && rows.length > 0) {
                    focusedIndex = Math.max(focusedIndex - 1, 0);
                    updateFocusedRow(rows);
                }
                break;
            case 'Enter':
                if (!detailVisible && focusedIndex >= 0 && focusedIndex < rows.length) {
                    e.preventDefault();
                    rows[focusedIndex].click();
                }
                break;
            case 'Delete':
            case 'Backspace':
                if (!detailVisible && focusedIndex >= 0 && focusedIndex < rows.length) {
                    e.preventDefault();
                    var deleteBtn = rows[focusedIndex].querySelector('.btn-delete-row');
                    if (deleteBtn) deleteBtn.click();
                    if (focusedIndex >= rows.length - 1) focusedIndex = rows.length - 2;
                }
                break;
            case 'Escape':
                if (document.getElementById('keyboardHelp').classList.contains('visible')) {
                    hideKeyboardHelp();
                } else if (detailVisible) {
                    e.preventDefault();
                    document.getElementById('backToInbox').click();
                }
                break;
            case '?':
                if (!detailVisible) {
                    e.preventDefault();
                    showKeyboardHelp();
                }
                break;
        }
    }

    function updateFocusedRow(rows) {
        rows.forEach(function (r) { r.classList.remove('focused'); });
        if (focusedIndex >= 0 && focusedIndex < rows.length) {
            rows[focusedIndex].classList.add('focused');
            rows[focusedIndex].scrollIntoView({ block: 'nearest' });
        }
    }

    function showKeyboardHelp() {
        document.getElementById('keyboardHelp').classList.add('visible');
    }

    function hideKeyboardHelp() {
        document.getElementById('keyboardHelp').classList.remove('visible');
    }

    return { init: init };
})();
