# How to Apply for DO-178C Level A and NASA NPR 7150.2 Class A Certification

## Overview

**DO-178C Level A** and **NASA NPR 7150.2 Class A** are not certifications you "apply for" like a license. Instead, they are **compliance standards** that must be demonstrated through rigorous processes, documentation, and verification. The certification comes from **regulatory authorities** (FAA for DO-178C, NASA for NPR 7150.2) after they review and approve your software.

## The Certification Process

### DO-178C Level A (FAA/EASA)

**Who Certifies:**
- **FAA (Federal Aviation Administration)** - For US aircraft
- **EASA (European Union Aviation Safety Agency)** - For EU aircraft
- Certification is done through **Designated Engineering Representatives (DERs)** or **Authorized Representatives**

**Process:**
1. **Develop software** following DO-178C processes
2. **Generate all required documentation** (SRD, SDD, test plans, etc.)
3. **Conduct verification activities** (reviews, tests, coverage analysis)
4. **Compile Software Accomplishment Summary (SAS)**
5. **Submit to certification authority** as part of aircraft/engine certification
6. **Authority reviews** and approves (or requests changes)

**Key Point:** DO-178C certification is **part of aircraft certification**, not standalone software certification.

### NASA NPR 7150.2 Class A (NASA)

**Who Certifies:**
- **NASA Office of Safety and Mission Assurance (OSMA)**
- **NASA Engineering and Safety Center (NESC)**
- Certification is part of **mission approval process**

**Process:**
1. **Develop software** following NPR 7150.2 processes
2. **Generate all required documentation**
3. **Conduct verification and validation**
4. **Submit Software Development Plan (SDP)** for approval
5. **NASA reviews** during mission reviews
6. **Approval granted** as part of mission certification

**Key Point:** NPR 7150.2 compliance is **part of mission approval**, not standalone.

## Prerequisites Before Certification

### 1. Complete All Required Documentation

**DO-178C Level A Requirements:**
- ✅ Software Requirements Document (SRD)
- ✅ Software Design Document (SDD)
- ✅ Software Verification Plan (SVP)
- ✅ Software Verification Cases and Procedures (SVCP)
- ✅ Software Configuration Management Plan (SCMP)
- ✅ Software Quality Assurance Plan (SQAP)
- ✅ Software Accomplishment Summary (SAS)
- ✅ Problem Reports (all resolved)
- ✅ Requirements Traceability Matrix (RTM)
- ✅ Test Coverage Reports (100% statement, branch, MC/DC)
- ✅ Code Review Records
- ✅ Tool Qualification Data (if using qualified tools)

**NASA NPR 7150.2 Class A Requirements:**
- ✅ Software Development Plan (SDP)
- ✅ Software Requirements Specification (SRS)
- ✅ Software Design Description (SDD)
- ✅ Software Test Plan (STP)
- ✅ Software Test Description (STD)
- ✅ Software Test Report (STR)
- ✅ Software Configuration Management Plan (SCMP)
- ✅ Software Quality Assurance Plan (SQAP)
- ✅ Software Safety Plan (SSP)
- ✅ Problem/Anomaly Reports
- ✅ Requirements Traceability Matrix
- ✅ Test Coverage Reports
- ✅ Code Review Records

### 2. Achieve 100% Coverage

**DO-178C Level A:**
- ✅ 100% Statement Coverage
- ✅ 100% Branch Coverage
- ✅ 100% MC/DC Coverage (Modified Condition/Decision Coverage)
- ✅ All safety-critical code must meet these requirements

**NASA NPR 7150.2 Class A:**
- ✅ 100% Statement Coverage
- ✅ 100% Branch Coverage
- ✅ MC/DC Coverage for safety-critical code
- ✅ Path Coverage (where applicable)

### 3. Complete All Verification Activities

- ✅ All requirements verified
- ✅ All code reviewed by certified reviewers
- ✅ All tests passed
- ✅ All problem reports resolved
- ✅ All change requests approved
- ✅ Configuration baselines established

### 4. Tool Qualification (If Required)

If you use tools for:
- Requirements management
- Code generation
- Test execution
- Coverage analysis

These tools may need **qualification** per DO-330 (Tool Qualification).

## Step-by-Step: How to Get Certified

### Phase 1: Preparation (6-12 months)

1. **Engage Certification Consultant**
   - Hire DER (Designated Engineering Representative) or NASA consultant
   - They guide you through the process
   - Cost: $150K - $500K+ depending on scope

2. **Establish Processes**
   - Document all development processes
   - Create templates for all required documents
   - Set up configuration management
   - Establish quality assurance procedures

3. **Train Team**
   - DO-178C training for developers
   - Code review training
   - Test coverage training
   - Process training

### Phase 2: Development (12-24 months)

1. **Follow Processes**
   - Develop software following DO-178C/NPR 7150.2 processes
   - Generate all documentation as you go
   - Conduct reviews and tests continuously

2. **Maintain Traceability**
   - Link requirements to design
   - Link design to code
   - Link code to tests
   - Maintain RTM throughout

3. **Achieve Coverage**
   - Write tests to achieve 100% coverage
   - Verify MC/DC coverage
   - Document all coverage gaps

### Phase 3: Verification (6-12 months)

1. **Complete All Verification**
   - Requirements verification
   - Design verification
   - Code verification
   - Test verification

2. **Generate Reports**
   - Test coverage reports
   - Problem report summaries
   - Configuration management reports
   - Quality assurance reports

3. **Compile Documentation**
   - Assemble all required documents
   - Create Software Accomplishment Summary
   - Prepare for authority review

### Phase 4: Submission (3-6 months)

1. **Submit to Authority**
   - Submit as part of aircraft/mission certification
   - Include all documentation
   - Respond to authority questions

2. **Authority Review**
   - Authority reviews all documentation
   - May request additional information
   - May request changes

3. **Approval**
   - Authority approves software
   - Certification granted as part of overall certification

## Realistic Timeline

**Minimum Timeline: 2-3 years**
- Preparation: 6-12 months
- Development: 12-24 months
- Verification: 6-12 months
- Authority Review: 3-6 months

**Typical Timeline: 3-5 years** for a complete system

## Costs

**Typical Costs:**
- Consultant/DER: $150K - $500K+
- Training: $50K - $100K
- Tools (qualified): $100K - $500K+
- Documentation: $200K - $500K+
- Testing: $300K - $1M+
- **Total: $800K - $2.5M+** for a complete system

## Who Can Help

### DO-178C Consultants/DERs:
- **Collins Aerospace** (formerly Rockwell Collins)
- **Honeywell**
- **Safran**
- **Independent DERs** (search FAA DER directory)

### NASA Consultants:
- **NASA Engineering and Safety Center (NESC)**
- **Aerospace Corporation**
- **Independent NASA consultants**

### Certification Authorities:
- **FAA**: https://www.faa.gov/aircraft/air_cert/design_approvals
- **EASA**: https://www.easa.europa.eu/
- **NASA OSMA**: https://www.nasa.gov/offices/nesc/home/

## What You Need Right Now

### Immediate Steps:

1. **Complete Your Systems** ✅ (DONE!)
   - Requirements Traceability ✅
   - Problem Reporting ✅
   - Configuration Management ✅
   - Test Coverage ✅
   - Code Reviews ✅

2. **Start Using Them**
   - Begin tracking requirements
   - Start recording test coverage
   - Create baselines
   - Conduct code reviews

3. **Generate Real Data**
   - Create actual requirements
   - Link to real code
   - Achieve real coverage
   - Build certification history

4. **Engage Consultant**
   - Find a DER or NASA consultant
   - Get initial assessment
   - Plan certification path

5. **Identify Target Application**
   - What aircraft/mission will use this?
   - Certification is part of that certification
   - Can't certify software in isolation

## Key Insight

**You cannot certify software in isolation.** Certification is always part of:
- **Aircraft certification** (for DO-178C)
- **Mission approval** (for NASA NPR 7150.2)

You need:
1. A **specific application** (aircraft engine, flight control system, etc.)
2. A **certification authority** (FAA, EASA, NASA)
3. A **certification path** (part of larger system certification)

## Next Steps for HelloblueGK

1. **Continue Building Systems** ✅ (In Progress)
2. **Start Using for Real Projects**
   - Use systems for actual engine software
   - Build certification history
   - Generate real documentation

3. **Identify Target Application**
   - What specific engine/component will be certified?
   - What's the certification path?

4. **Engage Consultant**
   - Find DER or NASA consultant
   - Get assessment of current state
   - Plan certification approach

5. **Build Partnership**
   - Partner with aircraft manufacturer
   - Or partner with NASA for mission
   - Certification happens as part of that

## Resources

- **DO-178C Standard**: Purchase from RTCA (rtca.org)
- **NASA NPR 7150.2**: Available from NASA (nasa.gov)
- **FAA DER Directory**: https://www.faa.gov/other_visit/aviation_industry/designees_delegations/designee_types/der
- **EASA**: https://www.easa.europa.eu/

## Summary

**Certification is not a simple application process.** It's a **multi-year, multi-million dollar effort** that requires:
- Complete processes and documentation
- 100% test coverage
- Authority review and approval
- Part of larger system certification

**Your systems are the foundation** - now you need to:
1. Use them for real projects
2. Build certification history
3. Engage consultants
4. Identify target application
5. Follow the certification path

The good news: **You have the systems in place!** Now it's about using them properly and following the certification process.
