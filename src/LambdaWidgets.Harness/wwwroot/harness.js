// Everything a browser must do, and nothing more.
//
// The page reports which bound element was clicked and what is in the widget's form fields. Every
// rule about what that means - which action it was, what the event carries, what the console would
// strip - is server side in LambdaWidgets.Dashboard, which the test driver resolves too. A page
// that decided any of it would be a second implementation of the console, and the two would drift.

async function render(slot) {
  const id = slot.dataset.widget;
  slot.innerHTML = await (await fetch(`/widgets/${id}`)).text();
  bind(slot);
}

// A cwdb-action binds the element immediately before it, which is the console's rule and the one
// an author gets wrong. The server has already found them; this only has to reach the same
// elements in the same order.
function bind(slot) {
  const rendered = slot.querySelector('.rendered');
  if (!rendered) return;

  [...rendered.querySelectorAll('cwdb-action')].forEach((action, index) => {
    const bound = action.previousElementSibling;
    if (!bound) return;

    action.style.display = 'none';
    bound.style.cursor = 'pointer';
    bound.addEventListener('click', () => fire(slot, index, action));
  });
}

async function fire(slot, index, action) {
  const confirmation = action.getAttribute('confirmation');
  if (confirmation && !await ask(slot, confirmation)) return;

  // An html action shows its content; only a call goes back to the function.
  if (action.getAttribute('action') !== 'call') {
    slot.querySelector('.rendered').innerHTML = action.innerHTML;
    return;
  }

  const fields = {};
  slot.querySelectorAll('[name]').forEach(field => {
    if (field.type === 'checkbox' || field.type === 'radio') {
      if (field.checked) fields[field.name] = field.value || 'on';
    } else {
      fields[field.name] = field.value;
    }
  });

  slot.innerHTML = await (await fetch(`/widgets/${slot.dataset.widget}/actions/${index}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(fields)
  })).text();
  bind(slot);
}

// The console renders its own confirmation, and window.confirm is a native modal: it blocks every
// event the page would have received, so a harness that used one could not be driven past a
// confirmed action by anything. The markup is the harness's own rather than the widget's - a widget
// asks by writing a confirmation attribute and never draws the dialog.
function ask(slot, message) {
  return new Promise(resolve => {
    const panel = document.createElement('div');
    panel.className = 'confirm';
    panel.innerHTML =
      '<p></p><button type="button" data-confirm="yes">OK</button>' +
      '<button type="button" data-confirm="no">Cancel</button>';
    panel.querySelector('p').textContent = message;

    const answer = value => () => { panel.remove(); resolve(value); };
    panel.querySelector('[data-confirm=yes]').addEventListener('click', answer(true));
    panel.querySelector('[data-confirm=no]').addEventListener('click', answer(false));

    (slot.querySelector('.widget') || slot).appendChild(panel);
    panel.querySelector('[data-confirm=yes]').focus();
  });
}

document.querySelectorAll('.slot').forEach(render);
