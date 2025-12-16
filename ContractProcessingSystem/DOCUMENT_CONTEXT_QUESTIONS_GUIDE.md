# Document Context Questions Guide

## ?? Comprehensive Reference for AI Contract Queries

**Purpose**: This guide provides example questions you can ask the ContractProcessingSystem AI when you have a **Document ID** (context). With document context, the AI can provide precise, specific answers about that particular contract.

**Last Updated**: January 2025  
**System Version**: ContractProcessingSystem v1.0  
**Target Framework**: .NET 8, .NET 9

---

## ?? Why Document Context Matters

### ? Without Document ID:
```
Query: "What is the base salary?"
Result: Returns results from ALL employment contracts in the database
Problem: Ambiguous - which contract? which employee?
```

### ? With Document ID:
```
Query: "What is the base salary?"
Context: Document ID = abc-123-def-456 (Jessica Martinez's contract)
Result: "$145,000 per year for Jessica Martinez at Innovation Tech Solutions"
```

---

## ?? How to Use This Guide

### **API Endpoint Format**

```http
POST https://localhost:7004/api/aiagent/chat
Content-Type: application/json

{
  "message": "Your question here",
  "contextDocumentId": "abc-123-def-456-789"
}
```

### **Quick Reference Table**

| Contract Type | Best Questions | Example Document ID |
|---------------|----------------|---------------------|
| **Employment Contract** | Salary, benefits, PTO, remote work, equity | `03_Employment_Contract.txt` |
| **Service Agreement** | Payment terms, milestones, deliverables | `01_Software_Development_Agreement.txt` |
| **Consulting Agreement** | Retainer, hourly rate, scope of work | `02_Consulting_Services_Agreement.txt` |
| **Lease Agreement** | Rent, lease term, maintenance, utilities | `04_Commercial_Lease_Agreement.txt` |

---

## ?? Employment Contract Questions

### **?? Compensation & Salary**

#### Basic Salary Questions
```
? "What is the base salary?"
? "What is Jessica's total compensation package worth?"
? "How much is the signing bonus?"
? "What is the performance bonus structure?"
? "How is the salary paid? (monthly, bi-weekly, etc.)"
? "When is the first paycheck?"
```

**Expected Answer Format:**
```
Base Salary: $145,000 per year
Pay Frequency: Bi-weekly (26 pay periods)
Signing Bonus: $10,000 (paid with first paycheck)
Performance Bonus: Up to 15% of base salary ($21,750 max)
Total First Year: $155,000 - $176,750 (depending on performance)
```

#### Advanced Compensation Questions
```
? "Calculate the total first-year compensation including all bonuses"
? "What is the hourly equivalent of the salary?"
? "What happens to compensation during the probationary period?"
? "When is the next salary review?"
? "Is there a cost of living adjustment?"
```

---

### **?? Equity & Stock Options**

```
? "How many stock options are included?"
? "What is the vesting schedule?"
? "What is the strike price for the stock options?"
? "What happens to unvested options if I'm terminated?"
? "What is a 1-year cliff?"
? "Are there refresh grants?"
? "What happens to stock options if the company is acquired?"
? "What happens to my options if I quit after 18 months?"
```

**Expected Answer Format:**
```
Stock Options: 5,000 shares
Vesting Schedule: 4-year vest with 1-year cliff
- After 1 year: 1,250 shares vest (25%)
- After that: ~104 shares vest monthly (1/48th of total)
Acceleration: 25% of unvested options accelerate on termination without cause
Refresh Grants: Eligible for additional annual grants (subject to performance)
```

---

### **?? Benefits & Perks**

#### Health & Insurance
```
? "What health insurance is provided?"
? "When do benefits start?"
? "How much does the company pay for health insurance?"
? "What about dental and vision coverage?"
? "Is there coverage for family members?"
? "What is the employee premium cost?"
```

#### Retirement
```
? "What is the 401(k) match?"
? "When am I eligible for 401(k)?"
? "Is there a vesting schedule for the 401(k) match?"
? "What retirement plans are offered?"
```

#### Time Off
```
? "How much vacation time do I get?"
? "How many sick days are included?"
? "What are the company holidays?"
? "Is there personal time off?"
? "What is the total PTO (paid time off)?"
? "Do unused vacation days roll over?"
? "Can I take unpaid time off?"
```

**Expected Answer Format:**
```
Vacation: 20 days per year
Sick Days: 10 days per year
Holidays: 11 company holidays
Personal Days: 5 days per year
Total PTO: 46 days per year

Health Insurance:
- Medical, dental, vision (starts day 1)
- Company pays 80% of employee premium
- Family coverage available at employee cost

401(k):
- 5% company match
- Immediate eligibility
- Immediate vesting
```

---

### **?? Remote Work & Schedule**

```
? "How many days can I work from home?"
? "What is the remote work policy?"
? "Is there a hybrid work arrangement?"
? "What are the core hours I need to be available?"
? "Can I work fully remote?"
? "What are the standard work hours?"
? "Is the work schedule flexible?"
? "Do I need to be in the office on specific days?"
```

**Expected Answer Format:**
```
Remote Work: Up to 3 days per week from home
Office Days: Minimum 2 days per week in office
Core Hours: 10 AM - 3 PM (must be available)
Standard Hours: 40 hours per week
Flexible Schedule: Permitted with manager approval
Equipment: Company provides MacBook Pro and accessories
```

---

### **?? Professional Development**

```
? "What is the professional development budget?"
? "Can I attend conferences?"
? "Is certification reimbursement available?"
? "What training programs are offered?"
? "Is there a LinkedIn Learning subscription?"
? "Can I take courses during work hours?"
```

---

### **?? Legal Terms & Clauses**

#### Non-Compete
```
? "Is there a non-compete clause?"
? "How long is the non-compete period?"
? "What is the geographic scope of the non-compete?"
? "What industries are restricted?"
? "Is there compensation if the non-compete is enforced?"
? "Can I work for a competitor after leaving?"
```

**Expected Answer Format:**
```
Non-Compete: Yes
Duration: 12 months after employment ends
Geographic Scope: 50-mile radius
Industry Restriction: Same industry only
Compensation: Employer will pay 50% of base salary ($72,500) if enforced
Enforceability: Likely enforceable in Texas (reasonable scope and duration)
```

#### Confidentiality
```
? "What are the confidentiality obligations?"
? "How long does confidentiality last?"
? "What information is considered confidential?"
? "What happens if I breach confidentiality?"
```

#### Intellectual Property
```
? "Who owns the work I create?"
? "What is the IP assignment policy?"
? "Do side projects belong to the company?"
? "What about inventions made outside of work?"
```

---

### **?? Termination & Severance**

```
? "What type of employment is this? (at-will, contract, etc.)"
? "What is the notice period for termination?"
? "What severance do I get if laid off?"
? "What happens if I'm terminated without cause?"
? "What happens if Jessica is laid off after 18 months?"
? "What happens to my benefits when I'm terminated?"
? "How long is health insurance continued after termination?"
? "What happens to stock options if I'm fired?"
? "Do I get paid for unused vacation when I leave?"
```

**Expected Answer Format:**
```
Employment Type: At-Will
Notice Period: 2 weeks preferred (not required)

Severance (terminated without cause):
- Base Severance: 2 weeks per year of service
- For 18 months: ~3 weeks salary (~$8,365)
- Health Insurance: Continued for 3 months
- Stock Acceleration: 25% of unvested options vest immediately

Final Paycheck: Within 7 days of termination
Equipment Return: Within 3 days
Confidentiality: Continues indefinitely
Non-Compete: Applies for 12 months (with 50% salary compensation if enforced)
```

---

### **?? Important Dates**

```
? "When does employment start?"
? "What is the probationary period?"
? "When does the contract expire?"
? "When is my first performance review?"
? "When do I become eligible for benefits?"
? "When can I start taking vacation?"
```

---

### **?? Job Details**

```
? "What is the job title?"
? "What department am I in?"
? "Who do I report to?"
? "What are my primary responsibilities?"
? "What are the key deliverables?"
? "Is there an on-call rotation?"
? "What is expected during the probationary period?"
```

---

## ?? Service/Development Contract Questions

### **?? Payment & Pricing**

```
? "What is the total contract value?"
? "What are the payment terms?"
? "What are the milestone payments?"
? "When is payment due?"
? "Are there any late payment penalties?"
? "What is the payment schedule?"
? "How much is each milestone worth?"
```

**Example Answer:**
```
Total Contract Value: $120,000
Payment Structure: Milestone-based
Milestones:
- Phase 1 (Requirements): $25,000 (upon completion)
- Phase 2 (Design): $30,000 (upon approval)
- Phase 3 (Development): $45,000 (upon delivery)
- Phase 4 (Testing): $20,000 (upon acceptance)
Payment Terms: Net 15 days
Late Payment: 1.5% monthly interest
```

---

### **?? Deliverables & Scope**

```
? "What deliverables are required?"
? "What is the project scope?"
? "What are the acceptance criteria?"
? "What is the timeline for each phase?"
? "What happens if deliverables are rejected?"
? "Are there change order procedures?"
```

---

### **? Timeline & Deadlines**

```
? "When does the project start?"
? "When is the project due?"
? "What are the milestone deadlines?"
? "What happens if deadlines are missed?"
? "Is there a penalty for late delivery?"
```

---

### **??? Warranties & Support**

```
? "What warranties are provided?"
? "Is there a warranty period?"
? "What post-delivery support is included?"
? "What is the bug fix policy?"
? "Is there ongoing maintenance?"
```

---

## ?? Consulting Contract Questions

### **?? Engagement Terms**

```
? "What is the monthly retainer?"
? "What is the hourly rate?"
? "How many hours are included in the retainer?"
? "What is the billing rate for hours beyond the retainer?"
? "What is the maximum monthly billing?"
? "What is the total annual value?"
```

**Example Answer:**
```
Monthly Retainer: $15,000
Included Hours: Minimum 40 hours per month
Hourly Rate (beyond retainer): $350/hour
Monthly Cap: $25,000 maximum
Annual Value: $180,000 - $300,000 (depending on usage)
```

---

### **?? Scope of Services**

```
? "What services are included?"
? "What are the consultant's responsibilities?"
? "What deliverables are required?"
? "How often are status reports due?"
? "Are there on-site requirements?"
```

---

## ?? Lease Agreement Questions

### **?? Rent & Payments**

```
? "What is the monthly rent?"
? "When is rent due?"
? "What is the security deposit?"
? "Are there any additional fees?"
? "What utilities are included?"
? "Is there a rent escalation clause?"
```

---

### **?? Lease Term**

```
? "When does the lease start?"
? "When does the lease end?"
? "What is the lease term?"
? "Is there an option to renew?"
? "What is the notice period for renewal?"
? "Can the lease be terminated early?"
```

---

### **?? Maintenance & Repairs**

```
? "Who is responsible for maintenance?"
? "What repairs are the landlord's responsibility?"
? "What repairs are the tenant's responsibility?"
? "Is there an emergency contact?"
```

---

## ?? Analysis & Risk Assessment Questions

### **Deep Dive Analysis**

```
? "Analyze this contract and identify risks"
? "What are the key terms I should negotiate?"
? "Is this a standard contract?"
? "What are the strengths and weaknesses?"
? "What should I watch out for?"
? "How does this compare to industry standards?"
? "What are unusual or concerning clauses?"
? "What are the biggest risks in this contract?"
```

---

### **Comparison Questions**

```
? "Compare the payment terms to typical industry rates"
? "Is the severance package competitive?"
? "How does this compare to a consulting agreement?"
? "Is the compensation fair for this role?"
```

---

### **Legal Review Questions**

```
? "What law governs this contract?"
? "Is there an arbitration clause?"
? "What is the dispute resolution process?"
? "Are there any indemnification clauses?"
? "What are the termination conditions?"
? "Is the non-compete enforceable?"
```

---

## ?? Multi-Document Comparison

### **Compare Multiple Contracts**

**API Endpoint:**
```http
POST https://localhost:7004/api/aiagent/compare
Content-Type: application/json

{
  "documentIds": [
    "abc-123-def-456",
    "xyz-789-ghi-012"
  ]
}
```

**Example Questions:**
```
? "Compare the salaries in these two employment contracts"
? "What are the differences between these contracts?"
? "Which contract has better terms?"
? "Compare the termination clauses"
? "Which contract offers better benefits?"
? "What are the payment term differences?"
```

---

## ?? Query Templates by Use Case

### **1. HR/Recruiting Team**

**Template Questions:**
```
?? Compensation Package:
"What is the total compensation for this position?"
"Break down all compensation components"
"What is included in the benefits package?"

?? Candidate Evaluation:
"How competitive is this offer compared to market rates?"
"What are the unique selling points of this package?"
"What negotiation opportunities exist?"

?? Onboarding:
"When do benefits start?"
"What equipment is provided?"
"What is the new hire schedule?"
```

---

### **2. Legal Review Team**

**Template Questions:**
```
?? Risk Assessment:
"What are the legal risks in this contract?"
"Are there any unusual clauses?"
"What clauses need negotiation?"

?? Compliance:
"Does this comply with [state] employment law?"
"Are there any problematic provisions?"
"What is the enforceability of the non-compete?"

?? Protection:
"What protections does the company have?"
"What liabilities does the company accept?"
"Are there adequate indemnification clauses?"
```

---

### **3. Finance/Compensation Team**

**Template Questions:**
```
?? Budget Planning:
"What is the total first-year cost for this employee?"
"What are the recurring costs (benefits, etc.)?"
"What is the maximum potential payout?"

?? Cost Analysis:
"Break down all financial obligations"
"What are the variable compensation components?"
"When are payments due?"

?? Forecasting:
"What is the year-over-year cost increase?"
"What is the 5-year total cost?"
"What are potential bonus payouts?"
```

---

### **4. Employee/Candidate**

**Template Questions:**
```
?? Understanding the Offer:
"Explain my total compensation package"
"What are all the benefits I receive?"
"What happens during the probationary period?"

?? Work-Life Balance:
"How much flexibility do I have?"
"What is the vacation policy?"
"Can I work remotely?"

?? Career Growth:
"What professional development is available?"
"When is my first performance review?"
"What is the promotion path?"

?? Protections:
"What happens if I'm laid off?"
"What severance do I get?"
"What happens to my stock options if I leave?"
```

---

## ?? Advanced Query Techniques

### **1. Multi-Part Questions**

```
? "What is the base salary, and how does it compare to the industry average for this role?"
? "Explain the vesting schedule and calculate how many shares I'll have after 2 years"
? "What are the termination terms, and what would I receive if laid off after 3 years?"
```

---

### **2. Scenario-Based Questions**

```
? "If I quit after 18 months, what happens to my stock options and severance?"
? "If the company is acquired in year 2, what happens to my unvested stock?"
? "If I'm terminated for cause, what do I lose?"
? "If I work 60 hours/week, what is my effective hourly rate?"
```

---

### **3. Calculation Questions**

```
? "Calculate my total take-home after taxes (assuming 30% tax rate)"
? "What is the net present value of my stock options?"
? "Calculate the hourly equivalent of my salary including all benefits"
? "If I max out the 401(k) match, how much do I contribute and receive?"
```

---

### **4. Comparison to Standards**

```
? "Is this compensation competitive for a Senior Cloud Engineer in Texas?"
? "How does this severance compare to industry standards?"
? "Is a 50-mile non-compete reasonable?"
? "Are 46 PTO days above or below average?"
```

---

## ? Questions You CANNOT Ask (Without Context)

If you **don't** provide a Document ID, these questions will fail or return ambiguous results:

```
? "What is the base salary?" ? Returns results from ALL employment contracts
? "What are the payment terms?" ? Too vague without context
? "When does the contract expire?" ? Which contract?
? "What is the monthly rent?" ? Which lease?
? "Who are the parties?" ? Multiple contracts have different parties
```

**Solution**: Always provide a Document ID when asking specific questions.

---

## ?? Pro Tips for Best Results

### **1. Be Specific**
? **Good**: "What is the severance policy if the employee is laid off?"  
? **Bad**: "Tell me about severance"

### **2. Ask Follow-Up Questions**
The system remembers previous conversation context:
```
Q1: "What is the base salary?"
A1: "$145,000 per year"

Q2: "What about bonuses?"  ? System knows you're still talking about same contract
A2: "In addition to the base salary of $145,000, Jessica is eligible for..."
```

### **3. Use Natural Language**
```
? "How much vacation time do I get?"
? "What happens if I get fired?"
? "Can I work from home?"
```

### **4. Request Calculations**
```
? "Calculate my total first-year earnings including maximum bonus"
? "If I work here for 3 years, how many stock options will vest?"
? "What is my effective hourly rate?"
```

### **5. Ask for Explanations**
```
? "Explain what a 1-year cliff means"
? "What does at-will employment mean?"
? "Help me understand the vesting schedule"
```

---

## ?? Expected Response Quality

### **Response Time**
- Simple queries (salary, dates): **~2-6 seconds**
- Complex analysis (risk assessment): **~9-16 seconds**
- Multi-document comparison: **~15-30 seconds**

### **Accuracy Levels**

| Query Type | Expected Accuracy | Notes |
|------------|-------------------|-------|
| **Basic Facts** (salary, dates) | 95-98% | Direct extraction from contract |
| **Financial Calculations** | 90-95% | May require manual verification |
| **Legal Interpretation** | 85-92% | Should be reviewed by attorney |
| **Risk Assessment** | 80-90% | Subjective, context-dependent |
| **Comparisons** | 85-93% | Depends on available data |

---

## ?? Sample Conversations

### **Example 1: New Employee Questions**

```
User: "I just received this offer. Help me understand the compensation package."
Context: employment_contract_abc123

AI: "I'd be happy to help you understand the offer from Innovation Tech Solutions!

**Base Compensation:**
- Salary: $145,000/year (bi-weekly payments)
- Signing Bonus: $10,000 (with first paycheck)
- Performance Bonus: Up to 15% ($21,750 max)

**Total First Year Cash: $155,000 - $176,750**

**Equity:**
- 5,000 stock options
- 4-year vesting with 1-year cliff
- After 1 year: 25% vests (1,250 shares)
- After that: Monthly vesting (~104 shares/month)

**Benefits (value ~$15,000/year):**
- Health insurance (80% employer-paid)
- 401(k) with 5% match (~$7,250/year)
- $3,000 education budget
- 46 total PTO days

**Work Flexibility:**
- Remote 3 days/week
- Flexible schedule with core hours

This is a competitive package for a Senior Cloud Engineer. Would you like me to
explain any specific component in more detail?"
```

---

### **Example 2: Severance Scenario**

```
User: "If I get laid off after 2.5 years, what do I get?"
Context: employment_contract_abc123

AI: "If you're terminated **without cause** after 2.5 years, here's what you receive:

**Severance Payment:**
- Formula: 2 weeks per year of service
- Calculation: 2.5 years × 2 weeks = 5 weeks
- Amount: (5/52) × $145,000 = **$13,942**

**Continued Benefits:**
- Health insurance for 3 months (~$2,250 value)

**Stock Option Acceleration:**
- Normal vesting at 2.5 years: 56.25% (2,813 shares)
- Acceleration: Additional 25% of unvested portion
- Unvested: 2,187 shares × 25% = 547 shares accelerate
- **Total vested: 3,360 shares (67.2% of 5,000)**

**Non-Compete:**
- Applies for 12 months after termination
- Employer must pay 50% salary ($72,500) if enforced

**Other:**
- Final paycheck within 7 days
- Paid for unused vacation
- Must return equipment within 3 days

**Total Estimated Package:**
- Cash severance: $13,942
- Benefits: $2,250
- Accelerated stock: ~547 shares (value depends on company valuation)
- Potential non-compete pay: $72,500 (if enforced)

The severance formula (2 weeks/year) is below industry average (4-6 weeks typical),
but the stock acceleration and non-compete compensation are quite generous."
```

---

### **Example 3: Contract Comparison**

```
User: "Compare these two employment contracts"
Context: [contract_a_abc123, contract_b_xyz789]

AI: "Here's a comparison of the two employment offers:

**BASE COMPENSATION:**
Contract A (Innovation Tech): $145,000
Contract B (Enterprise Corp): $160,000
Winner: Contract B (+$15,000/year)

**BONUSES:**
Contract A: 15% ($21,750 max)
Contract B: 10% ($16,000 max)
Winner: Contract A (+$5,750 potential)

**EQUITY:**
Contract A: 5,000 options, 4yr/1yr cliff
Contract B: 7,500 RSUs, 4yr/1yr cliff
Winner: Contract B (RSUs generally better than options)

**BENEFITS:**
Contract A: 46 PTO days, 80% health, 5% 401k match
Contract B: 35 PTO days, 100% health, 4% 401k match
Winner: Contract A (better overall package)

**REMOTE WORK:**
Contract A: 3 days/week
Contract B: 5 days/week (full remote)
Winner: Contract B (more flexibility)

**SEVERANCE:**
Contract A: 2 weeks/year
Contract B: 4 weeks/year
Winner: Contract B (industry standard)

**RECOMMENDATION:**
Contract B offers higher base pay and better severance, while Contract A has
better benefits and work-life balance. Your choice depends on priorities:
- Choose A if: You value PTO, benefits, and in-person collaboration
- Choose B if: You prioritize salary, equity, and full remote flexibility"
```

---

## ?? Additional Resources

### **Related Documents**
- `README.md` - System architecture overview
- `CONFIGURATION_GUIDE.md` - Setup instructions
- `EMPLOYMENT_CONTRACT_PROCESSING_ANALYSIS.md` - Detailed employment contract analysis
- `CONSULTING_PROCESSING_ANALYSIS.md` - Consulting agreement analysis
- `SOFTWARE_DEV_PROCESSING_ANALYSIS.md` - Development contract analysis
- `LEASE_PROCESSING_ANALYSIS.md` - Lease agreement analysis

### **API Endpoints**
- `POST /api/aiagent/chat` - Ask questions with document context
- `POST /api/aiagent/analyze/{documentId}` - Get comprehensive analysis
- `POST /api/aiagent/compare` - Compare multiple documents
- `POST /api/query/search` - Semantic search across documents

---

## ? Quick Reference Checklist

Before asking a question, verify:

- [ ] You have the correct Document ID
- [ ] Your question is specific (not "What about payment?" but "What is the monthly payment amount?")
- [ ] You're asking about information likely to be in the contract
- [ ] You're using natural language (the AI understands conversational queries)
- [ ] You're ready to ask follow-up questions if needed

---

## ?? Troubleshooting

### **"I'm not getting good answers"**

**Check:**
1. ? Is your Document ID correct?
2. ? Is the document fully processed? (Status should be "ProcessingComplete")
3. ? Is your question specific enough?
4. ? Are you asking about information that exists in the contract?

**Try:**
- Break complex questions into simpler parts
- Ask for clarification if the answer is unclear
- Use follow-up questions to refine the response

---

### **"The AI says it doesn't have enough information"**

**This means:**
- The information might not be in the contract
- The contract needs to be parsed/chunked first
- Your question might be too vague

**Try:**
- Ask if the information exists: "Does this contract mention [topic]?"
- Request a general summary first: "Summarize this contract"
- Be more specific: Instead of "payment", ask "monthly payment amount"

---

## ?? Best Practices Summary

1. **Always provide Document ID for specific questions**
2. **Start broad, then narrow**: "Summarize benefits" ? "What is the 401(k) match?"
3. **Use follow-up questions** to dive deeper
4. **Request calculations** when needed
5. **Ask for explanations** of complex terms
6. **Compare to industry standards** for context
7. **Verify critical information** manually (especially financial/legal terms)

---

## ?? Support

For technical issues or questions about the ContractProcessingSystem:
1. Check system logs in each microservice
2. Verify configuration in `appsettings.json`
3. Test with simple queries first
4. Review the `SOLUTION_SUMMARY.md` for architecture details

---

**Document Version**: 1.0  
**Last Updated**: January 2025  
**Author**: ContractProcessingSystem Documentation Team  
**Target Users**: HR Teams, Legal Teams, Finance Teams, Employees, Developers

---

*This guide is part of the ContractProcessingSystem documentation. For the latest updates and additional resources, refer to the project README and other analysis documents.*
