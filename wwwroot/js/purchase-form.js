// Purchase bill form: item cards, automatic totals, retail/wholesale rates, inline "new product".
(function () {
  const items = document.getElementById('items');
  const template = document.getElementById('rowTemplate');
  const form = document.getElementById('billForm');
  if (!items || !template || !form) return;

  const showStock = form.dataset.showStock === '1';
  const round = (n, d) => Math.round((n + Number.EPSILON) * Math.pow(10, d)) / Math.pow(10, d);
  const num = (v) => { const n = parseFloat(v); return isNaN(n) ? 0 : n; };
  const rows = () => items.querySelectorAll('.item-row');

  function rateType() {
    const c = form.querySelector('input[name="Form.RateType"]:checked');
    return c ? c.value : 'Retail';
  }

  // Wholesale falls back to the retail rate when the product has no wholesale rate.
  function rateFor(opt) {
    const retail = num(opt.dataset.rate);
    const wholesale = num(opt.dataset.wholesale);
    return rateType() === 'Wholesale' && wholesale > 0 ? wholesale : retail;
  }

  function reindex() {
    rows().forEach((row, i) => {
      row.querySelectorAll('[name]').forEach(el => {
        el.name = el.name.replace(/Items\[(\d+|__i__)\]/, 'Items[' + i + ']');
      });
    });
  }

  function recalc() {
    let kg = 0, amount = 0;
    rows().forEach(row => {
      const q = round(num(row.querySelector('.qty').value), 3);
      const r = round(num(row.querySelector('.rate').value), 2);
      const line = round(q * r, 2);
      row.querySelector('.line-total').textContent = line.toFixed(2);
      kg += q; amount += line;
    });
    document.getElementById('totalKg').textContent = round(kg, 3).toFixed(3);
    document.getElementById('totalAmount').textContent = round(amount, 2).toFixed(2);
  }

  // function updateLabels() {
  //   const wholesale = rateType() === 'Wholesale';
  //   rows().forEach(row => {
  //     row.querySelector('.rate-label').textContent = 'Rate / kg (' + (wholesale ? 'wholesale' : 'retail') + ')';
  //     row.querySelector('.other-rate-label').textContent = (wholesale ? 'Retail' : 'Wholesale') + ' rate / kg (optional)';
  //   });
  // }

    function updateLabels() {
    const wholesale = rateType() === 'Wholesale';
    rows().forEach(row => {
      row.querySelector('.rate-label').textContent = 'Rate / kg (' + (wholesale ? 'wholesale' : 'retail') + ')';
      row.querySelector('.other-rate-label').textContent =
        (wholesale ? 'Retail' : 'Wholesale') + ' price for this new product (optional)';
      row.querySelector('.other-rate-help').textContent = wholesale
        ? 'The rate you enter below is saved as the wholesale price. If you leave this empty, it is also used as the retail price.'
        : 'The rate you enter below is saved as the retail price. If you leave this empty, wholesale buying will use the retail price.';
    });
  }

  function hintFor(opt) {
    const retail = num(opt.dataset.rate);
    const wholesale = num(opt.dataset.wholesale);
    let t = 'Retail ' + retail.toFixed(2) + ' · ' + (wholesale > 0 ? 'Wholesale ' + wholesale.toFixed(2) : 'Wholesale not set');
    if (showStock && opt.dataset.stock) t = 'In stock: ' + opt.dataset.stock + ' kg · ' + t;
    return t;
  }

  function onProductChange(row) {
    const sel = row.querySelector('.product-select');
    const isNew = sel.value === '0';
    row.querySelector('.new-product').classList.toggle('d-none', !isNew);
    const hint = row.querySelector('.stock-hint');
    const opt = sel.options[sel.selectedIndex];
    const rate = row.querySelector('.rate');
    if (!isNew && sel.value) {
      // Fill the rate unless the user typed one by hand (or it came from a saved draft).
      if (!rate.dataset.manual || !rate.value) {
        const r = rateFor(opt);
        if (r > 0) { rate.value = r; delete rate.dataset.manual; }
      }
      hint.textContent = hintFor(opt);
    } else {
      hint.textContent = '';
    }
    recalc();
  }

  function addRow(item) {
    const html = template.innerHTML.replace(/__i__/g, rows().length);
    items.insertAdjacentHTML('beforeend', html);
    const row = items.lastElementChild;
    if (item) {
      if (item.productId !== null && item.productId !== undefined) row.querySelector('.product-select').value = String(item.productId);
      if (item.newProductName) row.querySelector('[name$=".NewProductName"]').value = item.newProductName;
      if (item.newCategoryId) row.querySelector('[name$=".NewCategoryId"]').value = String(item.newCategoryId);
      if (item.newOtherRatePerKg) row.querySelector('[name$=".NewOtherRatePerKg"]').value = item.newOtherRatePerKg;
      if (item.quantityKg !== null && item.quantityKg !== undefined) row.querySelector('.qty').value = item.quantityKg;
      if (item.ratePerKg !== null && item.ratePerKg !== undefined) {
        const rate = row.querySelector('.rate');
        rate.value = item.ratePerKg;
        rate.dataset.manual = '1';          // a saved rate is kept when the page opens
      }
    }
    updateLabels();
    onProductChange(row);
    return row;
  }

  // Switching Retail / Wholesale refills every line whose rate was not typed by hand.
  form.querySelectorAll('input[name="Form.RateType"]').forEach(r => r.addEventListener('change', () => {
    updateLabels();
    rows().forEach(row => {
      const sel = row.querySelector('.product-select');
      if (!sel.value || sel.value === '0') return;
      const rate = row.querySelector('.rate');
      if (rate.dataset.manual) return;
      const v = rateFor(sel.options[sel.selectedIndex]);
      if (v > 0) rate.value = v;
    });
    recalc();
  }));

  items.addEventListener('change', e => { if (e.target.classList.contains('product-select')) onProductChange(e.target.closest('.item-row')); });
  items.addEventListener('input', e => {
    if (e.target.classList.contains('rate')) e.target.dataset.manual = '1';
    if (e.target.classList.contains('qty') || e.target.classList.contains('rate')) recalc();
  });
  items.addEventListener('click', e => {
    const btn = e.target.closest('.remove-row');
    if (!btn) return;
    if (rows().length > 1) { btn.closest('.item-row').remove(); reindex(); recalc(); }
  });
  document.getElementById('addRow').addEventListener('click', () => addRow(null).querySelector('.product-select').focus());

  // Prevent double submission; Finalize asks first in a modal.
  // let submitted = false, confirmed = false;
  // form.addEventListener('submit', async e => {
  //   if (submitted) { e.preventDefault(); return; }
  //   const action = e.submitter && e.submitter.value;
  //   if (action === 'finalize' && !confirmed) {
  //     e.preventDefault();
  //     const submitter = e.submitter;
  //     const yes = await window.appConfirm({
  //       title: 'Finalize this bill?',
  //       text: 'Stock will be increased and the bill can no longer be edited.',
  //       button: 'Finalize & generate bill', cls: 'btn-dark'
  //     });
  //     if (yes) { confirmed = true; form.requestSubmit(submitter); }
  //     return;
  //   }
  //   reindex();
  //   submitted = true;
  //   form.querySelectorAll('button[type=submit]').forEach(b => { if (b !== e.submitter) b.disabled = true; });
  // });
  // Prevent double submission; Finalize asks first in a modal.
  let submitted = false, confirmed = false;
  form.addEventListener('submit', async e => {
    if (submitted) { e.preventDefault(); return; }
    const action = e.submitter && e.submitter.value;
    if (action === 'finalize' && !confirmed) {
      e.preventDefault();
      const submitter = e.submitter;
      const opts = {
        title: 'Finalize this bill?',
        text: 'Stock will be increased and the bill can no longer be edited.',
        button: 'Finalize & generate bill', cls: 'btn-dark'
      };
      let yes;
      try {
        yes = typeof window.appConfirm === 'function'
          ? await window.appConfirm(opts)
          : window.confirm(opts.title + '\n' + opts.text);   // fallback if the modal partial is missing
      } catch (err) {
        console.error('Confirm dialog failed', err);
        yes = window.confirm(opts.title + '\n' + opts.text);
      }
      if (yes) { confirmed = true; form.requestSubmit(submitter); }
      return;
    }
    reindex();
    submitted = true;
    form.querySelectorAll('button[type=submit]').forEach(b => { if (b !== e.submitter) b.disabled = true; });
  });
  const initial = (window.billInitialItems && window.billInitialItems.length) ? window.billInitialItems : [null];
  initial.forEach(addRow);
  recalc();
})();

// // Purchase bill form: dynamic item rows, automatic totals, retail/wholesale rates, inline "new product".
// (function () {
//   const tbody = document.querySelector('#itemsTable tbody');
//   const template = document.getElementById('rowTemplate');
//   const form = document.getElementById('billForm');
//   if (!tbody || !template || !form) return;

//   const round = (n, d) => Math.round((n + Number.EPSILON) * Math.pow(10, d)) / Math.pow(10, d);
//   const num = (v) => { const n = parseFloat(v); return isNaN(n) ? 0 : n; };

//   function rateType() {
//     const c = form.querySelector('input[name="Form.RateType"]:checked');
//     return c ? c.value : 'Retail';
//   }

//   // Wholesale falls back to the retail rate when the product has no wholesale rate.
//   function rateFor(opt) {
//     const retail = num(opt.dataset.rate);
//     const wholesale = num(opt.dataset.wholesale);
//     return rateType() === 'Wholesale' && wholesale > 0 ? wholesale : retail;
//   }

//   function reindex() {
//     tbody.querySelectorAll('tr.item-row').forEach((tr, i) => {
//       tr.querySelectorAll('[name]').forEach(el => {
//         el.name = el.name.replace(/Items\[(\d+|__i__)\]/, 'Items[' + i + ']');
//       });
//     });
//   }

//   function recalc() {
//     let kg = 0, amount = 0;
//     tbody.querySelectorAll('tr.item-row').forEach(tr => {
//       const q = round(num(tr.querySelector('.qty').value), 3);
//       const r = round(num(tr.querySelector('.rate').value), 2);
//       const line = round(q * r, 2);
//       tr.querySelector('.line-total').textContent = line.toFixed(2);
//       kg += q; amount += line;
//     });
//     document.getElementById('totalKg').textContent = round(kg, 3).toFixed(3);
//     document.getElementById('totalAmount').textContent = round(amount, 2).toFixed(2);
//   }

//   function hintFor(opt) {
//     const retail = num(opt.dataset.rate);
//     const wholesale = num(opt.dataset.wholesale);
//     let t = 'In stock: ' + opt.dataset.stock + ' kg · Retail ' + retail.toFixed(2);
//     t += wholesale > 0 ? ' · Wholesale ' + wholesale.toFixed(2) : ' · Wholesale not set';
//     return t;
//   }

//   function onProductChange(tr) {
//     const sel = tr.querySelector('.product-select');
//     const isNew = sel.value === '0';
//     tr.querySelector('.new-product').classList.toggle('d-none', !isNew);
//     const hint = tr.querySelector('.stock-hint');
//     const opt = sel.options[sel.selectedIndex];
//     const rate = tr.querySelector('.rate');
//     if (!isNew && sel.value) {
//       // Fill the rate unless the user typed one by hand (or it came from a saved draft).
//       if (!rate.dataset.manual || !rate.value) {
//         const r = rateFor(opt);
//         if (r > 0) { rate.value = r; delete rate.dataset.manual; }
//       }
//       hint.textContent = hintFor(opt);
//     } else {
//       hint.textContent = '';
//     }
//     recalc();
//   }

//   function addRow(item) {
//     const html = template.innerHTML.replace(/__i__/g, tbody.querySelectorAll('tr.item-row').length);
//     tbody.insertAdjacentHTML('beforeend', html);
//     const tr = tbody.lastElementChild;
//     if (item) {
//       if (item.productId !== null && item.productId !== undefined) tr.querySelector('.product-select').value = String(item.productId);
//       if (item.newProductName) tr.querySelector('[name$=".NewProductName"]').value = item.newProductName;
//       if (item.newCategoryId) tr.querySelector('[name$=".NewCategoryId"]').value = String(item.newCategoryId);
//       if (item.quantityKg !== null && item.quantityKg !== undefined) tr.querySelector('.qty').value = item.quantityKg;
//       if (item.ratePerKg !== null && item.ratePerKg !== undefined) {
//         const rate = tr.querySelector('.rate');
//         rate.value = item.ratePerKg;
//         rate.dataset.manual = '1';          // a saved rate is kept when the page opens
//       }
//     }
//     onProductChange(tr);
//     return tr;
//   }

//   // Switching Retail / Wholesale refills every line whose rate was not typed by hand.
//   form.querySelectorAll('input[name="Form.RateType"]').forEach(r => r.addEventListener('change', () => {
//     tbody.querySelectorAll('tr.item-row').forEach(tr => {
//       const sel = tr.querySelector('.product-select');
//       if (!sel.value || sel.value === '0') return;
//       const rate = tr.querySelector('.rate');
//       if (rate.dataset.manual) return;
//       const r = rateFor(sel.options[sel.selectedIndex]);
//       if (r > 0) rate.value = r;
//     });
//     recalc();
//   }));

//   tbody.addEventListener('change', e => { if (e.target.classList.contains('product-select')) onProductChange(e.target.closest('tr')); });
//   tbody.addEventListener('input', e => {
//     if (e.target.classList.contains('rate')) e.target.dataset.manual = '1';
//     if (e.target.classList.contains('qty') || e.target.classList.contains('rate')) recalc();
//   });
//   tbody.addEventListener('click', e => {
//     const btn = e.target.closest('.remove-row');
//     if (!btn) return;
//     if (tbody.querySelectorAll('tr.item-row').length > 1) { btn.closest('tr').remove(); reindex(); recalc(); }
//   });
//   document.getElementById('addRow').addEventListener('click', () => addRow(null).querySelector('.product-select').focus());

//   // Prevent double submission (double clicks must not send the bill twice).
//   // let submitted = false;
//   // form.addEventListener('submit', e => {
//   //   if (submitted) { e.preventDefault(); return; }
//   //   const action = e.submitter && e.submitter.value;
//   //   if (action === 'finalize' && !confirm('Finalize this bill? Stock will be increased and the bill can no longer be edited.')) { e.preventDefault(); return; }
//   //   reindex();
//   //   submitted = true;
//   //   form.querySelectorAll('button[type=submit]').forEach(b => { if (b !== e.submitter) b.disabled = true; });
//   // });

//     // Prevent double submission (double clicks must not send the bill twice).
//   let submitted = false, confirmed = false;
//   form.addEventListener('submit', async e => {
//     if (submitted) { e.preventDefault(); return; }
//     const action = e.submitter && e.submitter.value;
//     if (action === 'finalize' && !confirmed) {
//       e.preventDefault();
//       const submitter = e.submitter;
//       const yes = await window.appConfirm({
//         title: 'Finalize this bill?',
//         text: 'Stock will be increased and the bill can no longer be edited.',
//         button: 'Finalize & generate bill', cls: 'btn-dark'
//       });
//       if (yes) { confirmed = true; form.requestSubmit(submitter); }
//       return;
//     }
//     reindex();
//     submitted = true;
//     form.querySelectorAll('button[type=submit]').forEach(b => { if (b !== e.submitter) b.disabled = true; });
//   });

//   const initial = (window.billInitialItems && window.billInitialItems.length) ? window.billInitialItems : [null];
//   initial.forEach(addRow);
//   recalc();
// })();